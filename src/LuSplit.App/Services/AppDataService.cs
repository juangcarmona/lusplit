using LuSplit.Application.Commands;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.App.Resources.Localization;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Client;
using LuSplit.Infrastructure.Export;
using Microsoft.Maui.Storage;

namespace LuSplit.App.Services;

public sealed class AppDataService : IAsyncDisposable, IAddExpenseDataService, IExpenseDetailsDataService, IHomeDataService, IRecordPaymentDataService, IGroupDetailsDataService, IArchivedGroupDataService, ICreateGroupDataService, IGroupSwitcherDataService, IGroupPageDataService, IActivityDataService, IArchivedGroupsDataService
{
    private const string SelectedGroupPreferenceKey = "selected-group-id";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly GuidIdGenerator _idGenerator = new();
    private InfraLocalSqlite? _infra;
    private string? _lastPaidByParticipantId;
    private IReadOnlyList<string> _lastParticipantIds = Array.Empty<string>();
    private string? _selectedGroupId;
    private readonly ExpensePersistenceService _expenses;
    private readonly GroupPersistenceService _group;
    private readonly TransferPersistenceService _transfers;

    public event EventHandler? DataChanged;

    public AppDataService()
    {
        _expenses = new ExpensePersistenceService(GetInfraAsync, GetSelectedGroupIdAsync);
        _group = new GroupPersistenceService(GetInfraAsync, _idGenerator);
        _transfers = new TransferPersistenceService(GetInfraAsync, GetSelectedGroupIdAsync);
    }

    public async Task InitializeAsync()
    {
        if (_infra is not null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_infra is not null)
            {
                return;
            }

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lusplit.sqlite");
            _infra = await InfraLocalSqlite.CreateAsync(dbPath);
            await EnsureAppMetadataTablesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GroupOverviewModel> GetOverviewAsync()
        => (await GetGroupWorkspaceAsync()).Overview;

    public async Task<GroupOverviewModel> GetOverviewAsync(string groupId)
        => (await GetGroupWorkspaceAsync(groupId)).Overview;

    public async Task<GroupWorkspaceModel> GetGroupWorkspaceAsync()
        => await GetGroupWorkspaceAsync(await GetSelectedGroupIdAsync());

    public async Task<GroupWorkspaceModel> GetGroupWorkspaceAsync(string groupId)
    {
        var overview = await GetGroupOverviewAsync(groupId);
        var metadata = await _group.GetGroupMetadataAsync(groupId);
        return new GroupWorkspaceModel(
            groupId,
            ResolveGroupName(metadata.Name, overview),
            overview,
            await _expenses.GetExpenseIconsAsync(groupId),
            metadata.LastOpenedUtc,
            metadata.ImagePath);
    }

    public async Task<IReadOnlyList<ParticipantModel>> GetParticipantsAsync()
    {
        var overview = await GetGroupOverviewAsync(await GetSelectedGroupIdAsync());
        return overview.Participants;
    }

    public async Task<IReadOnlyList<GroupListItemModel>> GetGroupsAsync()
    {
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var groupIds = await _group.ListGroupIdsAsync();
        var groups = new List<GroupListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var overview = await GetGroupOverviewAsync(groupId);
            // Archived groups are hidden from the active list.
            if (overview.Group.Closed) continue;
            var metadata = await _group.GetGroupMetadataAsync(groupId);
            var groupName = ResolveGroupName(metadata.Name, overview);
            var lastActivity = GetLastActivity(overview);
            var rankDate = metadata.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            groups.Add(new GroupListItemModel(
                groupId,
                groupName,
                overview.Group.Currency,
                string.Equals(groupId, selectedGroupId, StringComparison.Ordinal),
                GroupPresentationMapper.BuildGroupSummary(overview),
                GroupPresentationMapper.BuildBalancePreview(overview, 1).FirstOrDefault() ?? AppResources.Status_AddFirstEvent,
                BuildGroupStatusText(string.Equals(groupId, selectedGroupId, StringComparison.Ordinal), metadata.LastOpenedUtc, lastActivity),
                rankDate,
                metadata.ImagePath));
        }

        return groups
            .OrderByDescending(group => group.IsCurrent)
            .ThenByDescending(group => group.RankDate)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync()
    {
        var groupIds = await _group.ListGroupIdsAsync();
        var groups = new List<GroupListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var overview = await GetGroupOverviewAsync(groupId);
            if (!overview.Group.Closed) continue;
            var metadata = await _group.GetGroupMetadataAsync(groupId);
            var groupName = ResolveGroupName(metadata.Name, overview);
            var lastActivity = GetLastActivity(overview);
            var rankDate = metadata.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            groups.Add(new GroupListItemModel(
                groupId,
                groupName,
                overview.Group.Currency,
                false,
                GroupPresentationMapper.BuildGroupSummary(overview),
                GroupPresentationMapper.BuildBalancePreview(overview, 1).FirstOrDefault() ?? AppResources.Status_Settled,
                string.Empty,
                rankDate,
                metadata.ImagePath));
        }

        return groups
            .OrderByDescending(t => t.RankDate)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GroupDetailsModel> GetGroupDetailsAsync()
        => await GetGroupDetailsAsync(await GetSelectedGroupIdAsync());

    public async Task<GroupDetailsModel> GetGroupDetailsAsync(string groupId)
    {
        var overview = await GetGroupOverviewAsync(groupId);
        var metadata = await _group.GetGroupMetadataAsync(groupId);
        var householdNames = BuildHouseholdLookup(overview);
        var ownerIdsByUnit = overview.EconomicUnits
            .ToDictionary(unit => unit.Id, unit => unit.OwnerParticipantId, StringComparer.Ordinal);

        return new GroupDetailsModel(
            groupId,
            ResolveGroupName(metadata.Name, overview),
            overview.Group.Currency,
            overview.Group.Closed,
            overview.Participants
                .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
                .Select(participant => new GroupMemberModel(
                    participant.Id,
                    participant.Name,
                    householdNames.TryGetValue(participant.EconomicUnitId, out var householdName) ? householdName : participant.Name,
                    ownerIdsByUnit.TryGetValue(participant.EconomicUnitId, out var ownerId) &&
                        string.Equals(ownerId, participant.Id, StringComparison.Ordinal),
                    participant.ConsumptionCategory.ToString().ToUpperInvariant(),
                    participant.CustomConsumptionWeight))
                .ToArray(),
            metadata.ImagePath);
    }

    /// <summary>Archives a group. Archived groups are read-only - the domain blocks new expenses and participants on closed groups.</summary>
    public async Task ArchiveGroupAsync(string groupId)
    {
        await _group.ArchiveGroupCoreAsync(groupId);

        // If the archived group was the active selection, fall through to the next active group.
        if (string.Equals(_selectedGroupId, groupId, StringComparison.Ordinal))
        {
            _selectedGroupId = null;
            Preferences.Default.Remove(SelectedGroupPreferenceKey);
        }

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public EventDraftDefaults GetEventDraftDefaults()
        => new(_lastPaidByParticipantId, _lastParticipantIds);

    public async Task SelectGroupAsync(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        _selectedGroupId = groupId.Trim();
        Preferences.Default.Set(SelectedGroupPreferenceKey, _selectedGroupId);
        await _group.TouchGroupAsync(_selectedGroupId);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Persists the group image path (or clears it when <paramref name="imagePath"/> is <c>null</c>).</summary>
    public async Task SaveGroupImageAsync(string groupId, string? imagePath)
    {
        await _group.SaveGroupImageAsync(groupId, imagePath);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string> CreateGroupAsync(string groupName, string currency, IReadOnlyList<GroupDraftMember> members)
    {
        var normalizedName = NormalizeRequired(groupName, AppResources.Validation_GroupNameRequired);
        var normalizedCurrency = NormalizeRequired(currency, AppResources.Validation_CurrencyRequired).ToUpperInvariant();

        var memberDrafts = members
            .Select(member => new GroupDraftMember(
                NormalizeRequired(member.Name, AppResources.Validation_EachPersonNeedsName),
                NormalizeOptional(member.HouseholdName),
                member.ConsumptionCategory,
                member.CustomConsumptionWeight))
            .ToArray();
        EnsureUniqueMemberNames(memberDrafts.Select(member => member.Name));

        if (memberDrafts.Length == 0)
        {
            throw new InvalidOperationException(AppResources.Validation_AddAtLeastOnePerson);
        }

        var infra = await GetInfraAsync();
        var group = await new CreateGroupUseCase(infra.GroupRepository, _idGenerator)
            .ExecuteAsync(new CreateGroupInput(normalizedCurrency));

        await _group.SaveGroupMetadataAsync(group.Id, normalizedName, DateTimeOffset.UtcNow);
        await _group.AddMembersAsync(group.Id, memberDrafts);
        await SelectGroupAsync(group.Id);
        return group.Id;
    }

    public async Task UpdateGroupAsync(string groupId, string groupName, string currency)
    {
        var normalizedName = NormalizeRequired(groupName, AppResources.Validation_GroupNameRequired);
        var normalizedCurrency = NormalizeRequired(currency, AppResources.Validation_CurrencyRequired).ToUpperInvariant();
        await _group.UpdateGroupAsync(groupId, normalizedName, normalizedCurrency);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddGroupMemberAsync(
        string groupId,
        string personName,
        string? householdName,
        ConsumptionCategory consumptionCategory = ConsumptionCategory.Full,
        string? customConsumptionWeight = null)
    {
        var normalizedName = NormalizeRequired(personName, AppResources.Validation_PersonNameRequired);
        var normalizedHouseholdName = NormalizeOptional(householdName);
        await _group.AddGroupMemberAsync(groupId, normalizedName, normalizedHouseholdName, consumptionCategory, customConsumptionWeight);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateGroupMemberAsync(string groupId, string participantId, string personName, string? dependsOnParticipantId)
    {
        var normalizedParticipantId = NormalizeRequired(participantId, AppResources.Validation_PersonNotFound);
        var normalizedName = NormalizeRequired(personName, AppResources.Validation_PersonNameRequired);
        var normalizedDependsOnParticipantId = NormalizeOptional(dependsOnParticipantId);
        await _group.UpdateGroupMemberAsync(groupId, normalizedParticipantId, normalizedName, normalizedDependsOnParticipantId);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddExpenseAsync(
        string title,
        long amountMinor,
        string paidByParticipantId,
        DateTime date,
        IReadOnlyList<string> participantIds,
        string? icon,
        SplitDefinition? splitDefinition = null)
    {
        await _expenses.AddExpenseAsync(title, amountMinor, paidByParticipantId, date, participantIds, icon, splitDefinition);

        _lastPaidByParticipantId = paidByParticipantId;
        _lastParticipantIds = participantIds.ToArray();

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPaymentAsync(string fromParticipantId, string toParticipantId, long amountMinor, DateTime date)
    {
        await _transfers.AddPaymentAsync(fromParticipantId, toParticipantId, amountMinor, date);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<ExpenseModel?> GetExpenseAsync(string expenseId)
        => _expenses.GetExpenseAsync(expenseId);

    public async Task UpdateExpenseAsync(
        string expenseId,
        string title,
        string paidByParticipantId,
        long amountMinor,
        DateTime date,
        SplitDefinition splitDefinition,
        string? notes)
    {
        await _expenses.UpdateExpenseAsync(expenseId, title, paidByParticipantId, amountMinor, date, splitDefinition, notes);

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteExpenseAsync(string expenseId)
    {
        await _expenses.DeleteExpenseAsync(expenseId);

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<(IReadOnlyList<BalanceModel> Balances, SettlementPlanModel Settlement)> GetSettlementAsync(SettlementMode mode)
    {
        var overview = await GetGroupOverviewAsync(await GetSelectedGroupIdAsync());
        return mode == SettlementMode.Participant
            ? (overview.BalancesByParticipant, overview.SettlementByParticipant)
            : (overview.BalancesByEconomicUnitOwner, overview.SettlementByEconomicUnitOwner);
    }

    public async Task<ExportFileResult> ExportGroupAsync(string groupId, ExportFormat format)
    {
        var workspace = await GetGroupWorkspaceAsync(groupId);
        var outputDir = FileSystem.CacheDirectory;
        var dto = new ExportGroupDto(
            groupId,
            workspace.GroupName,
            DateTimeOffset.UtcNow.ToString("O"),
            workspace.Overview,
            outputDir);

        var exporter = new GroupExporterService();
        return format switch
        {
            ExportFormat.Json => await exporter.ExportJsonAsync(dto),
            ExportFormat.Csv => await exporter.ExportCsvBundleAsync(dto),
            ExportFormat.Pdf => await exporter.ExportPdfAsync(dto),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_infra is not null)
        {
            _infra.Dispose();
            _infra = null;
        }

        _gate.Dispose();
        await Task.CompletedTask;
    }

    private async Task<InfraLocalSqlite> GetInfraAsync()
    {
        await InitializeAsync();
        return _infra ?? throw new InvalidOperationException("Infrastructure not initialized.");
    }

    private async Task<GroupOverviewModel> GetGroupOverviewAsync(string groupId)
    {
        var infra = await GetInfraAsync();
        return await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(groupId);
    }

    private async Task EnsureAppMetadataTablesAsync()
    {
        var infra = _infra ?? throw new InvalidOperationException("Infrastructure not initialized.");

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS group_metadata (
  group_id TEXT PRIMARY KEY NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
  name TEXT NULL,
  last_opened_utc TEXT NULL
);

CREATE TABLE IF NOT EXISTS expense_ui_metadata (
  expense_id TEXT PRIMARY KEY NOT NULL REFERENCES expenses(id) ON DELETE CASCADE,
  icon TEXT NULL
);";
        command.ExecuteNonQuery();

        // Migration: add image_path column if this is an existing database without it.
        try
        {
            using var alterCmd = infra.Db.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE group_metadata ADD COLUMN image_path TEXT NULL";
            alterCmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate column name"))
        {
            // Column already present from a previous run; nothing to do.
        }

        await Task.CompletedTask;
    }

    private async Task<string> GetSelectedGroupIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_selectedGroupId))
        {
            return _selectedGroupId;
        }

        var groupIds = await _group.ListGroupIdsAsync();
        if (groupIds.Count == 0)
        {
            throw new NoGroupsAvailableException();
        }

        var preferredGroupId = Preferences.Default.Get(SelectedGroupPreferenceKey, string.Empty);
        _selectedGroupId = groupIds.FirstOrDefault(groupId => string.Equals(groupId, preferredGroupId, StringComparison.Ordinal))
            ?? groupIds.First();

        Preferences.Default.Set(SelectedGroupPreferenceKey, _selectedGroupId);
        return _selectedGroupId;
    }

    private static Dictionary<string, string> BuildHouseholdLookup(GroupOverviewModel overview)
    {
        var participantsById = overview.Participants.ToDictionary(participant => participant.Id, participant => participant.Name, StringComparer.Ordinal);

        return overview.EconomicUnits.ToDictionary(
            unit => unit.Id,
            unit => !string.IsNullOrWhiteSpace(unit.Name)
                ? unit.Name!
                : participantsById.TryGetValue(unit.OwnerParticipantId, out var ownerName)
                    ? ownerName
                    : AppResources.Status_Household,
            StringComparer.Ordinal);
    }

    private static string ResolveGroupName(string? storedName, GroupOverviewModel overview)
    {
        if (!string.IsNullOrWhiteSpace(storedName))
        {
            return storedName.Trim();
        }

        var participantNames = overview.Participants
            .Select(participant => participant.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(3)
            .ToArray();

        return participantNames.Length switch
        {
            0 => AppResources.Status_DefaultGroupName,
            1 => participantNames[0],
            _ => string.Join(", ", participantNames.Take(participantNames.Length - 1)) + " " + AppResources.Mapper_And + " " + participantNames[^1]
        };
    }

    private static string BuildGroupStatusText(bool isCurrent, DateTimeOffset? lastOpenedUtc, DateTimeOffset? lastActivityUtc)
    {
        if (isCurrent)
        {
            return AppResources.Status_CurrentGroup;
        }

        if (lastOpenedUtc.HasValue)
        {
            return string.Format(AppResources.Status_OpenedOn, GroupPresentationMapper.DescribeDay(lastOpenedUtc.Value));
        }

        if (lastActivityUtc.HasValue)
        {
            return string.Format(AppResources.Status_RecentActivity, GroupPresentationMapper.DescribeDay(lastActivityUtc.Value));
        }

        return AppResources.Status_ReadyForFirstEvent;
    }

    private static DateTimeOffset? GetLastActivity(GroupOverviewModel overview)
    {
        var lastExpense = overview.Expenses.Select(expense => GroupPresentationMapper.ParseDate(expense.Date));
        var lastTransfer = overview.Transfers.Select(transfer => GroupPresentationMapper.ParseDate(transfer.Date));
        var latest = lastExpense.Concat(lastTransfer).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        return latest == DateTimeOffset.MinValue ? null : latest;
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureUniqueMemberNames(IEnumerable<string> names)
    {
        var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (!uniqueNames.Add(name))
            {
                throw new InvalidOperationException(AppResources.Validation_PersonNameMustBeUnique);
            }
        }
    }

}

sealed record GroupMetadataRecord(string? Name, DateTimeOffset? LastOpenedUtc, string? ImagePath = null);
