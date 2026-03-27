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

public sealed class AppDataService : IAsyncDisposable
{
    private const string SelectedGroupPreferenceKey = "selected-group-id";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly GuidIdGenerator _idGenerator = new();
    private InfraLocalSqlite? _infra;
    private string? _lastPaidByParticipantId;
    private IReadOnlyList<string> _lastParticipantIds = Array.Empty<string>();
    private string? _selectedGroupId;

    public event EventHandler? DataChanged;

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
        var infra = await GetInfraAsync();
        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(groupId);

        var metadata = await GetGroupMetadataAsync(groupId);
        return new GroupWorkspaceModel(
            groupId,
            ResolveGroupName(metadata.Name, overview),
            overview,
            await GetExpenseIconsAsync(groupId),
            metadata.LastOpenedUtc,
            metadata.ImagePath);
    }

    public async Task<IReadOnlyList<ParticipantModel>> GetParticipantsAsync()
    {
        var overview = await GetOverviewAsync();
        return overview.Participants;
    }

    public async Task<IReadOnlyList<GroupListItemModel>> GetGroupsAsync()
    {
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var groupIds = await ListGroupIdsAsync();
        var groups = new List<GroupListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var workspace = await GetGroupWorkspaceAsync(groupId);
            // Archived groups are hidden from the active list.
            if (workspace.Overview.Group.Closed) continue;
            var lastActivity = GetLastActivity(workspace.Overview);
            var rankDate = workspace.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            groups.Add(new GroupListItemModel(
                groupId,
                workspace.GroupName,
                workspace.Overview.Group.Currency,
                string.Equals(groupId, selectedGroupId, StringComparison.Ordinal),
                GroupPresentationMapper.BuildGroupSummary(workspace.Overview),
                GroupPresentationMapper.BuildBalancePreview(workspace.Overview, 1).FirstOrDefault() ?? AppResources.Status_AddFirstEvent,
                BuildGroupStatusText(string.Equals(groupId, selectedGroupId, StringComparison.Ordinal), workspace.LastOpenedUtc, lastActivity),
                rankDate,
                workspace.ImagePath));
        }

        return groups
            .OrderByDescending(group => group.IsCurrent)
            .ThenByDescending(group => group.RankDate)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<GroupListItemModel>> GetArchivedGroupsAsync()
    {
        var groupIds = await ListGroupIdsAsync();
        var groups = new List<GroupListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var workspace = await GetGroupWorkspaceAsync(groupId);
            if (!workspace.Overview.Group.Closed) continue;
            var lastActivity = GetLastActivity(workspace.Overview);
            var rankDate = workspace.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            groups.Add(new GroupListItemModel(
                groupId,
                workspace.GroupName,
                workspace.Overview.Group.Currency,
                false,
                GroupPresentationMapper.BuildGroupSummary(workspace.Overview),
                GroupPresentationMapper.BuildBalancePreview(workspace.Overview, 1).FirstOrDefault() ?? AppResources.Status_Settled,
                string.Empty,
                rankDate,
                workspace.ImagePath));
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
        var workspace = await GetGroupWorkspaceAsync(groupId);
        var householdNames = BuildHouseholdLookup(workspace.Overview);
        var ownerIdsByUnit = workspace.Overview.EconomicUnits
            .ToDictionary(unit => unit.Id, unit => unit.OwnerParticipantId, StringComparer.Ordinal);

        return new GroupDetailsModel(
            groupId,
            workspace.GroupName,
            workspace.Overview.Group.Currency,
            workspace.Overview.Group.Closed,
            workspace.Overview.Participants
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
            workspace.ImagePath);
    }

    /// <summary>Archives a group. Archived groups are read-only - the domain blocks new expenses and participants on closed groups.</summary>
    public async Task ArchiveGroupAsync(string groupId)
    {
        var infra = await GetInfraAsync();
        await new CloseGroupUseCase(infra.GroupRepository).ExecuteAsync(new CloseGroupInput(groupId));

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
        await TouchGroupAsync(_selectedGroupId);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Persists the group image path (or clears it when <paramref name="imagePath"/> is <c>null</c>).</summary>
    public async Task SaveGroupImageAsync(string groupId, string? imagePath)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO group_metadata (group_id, image_path)
VALUES ($groupId, $imagePath)
ON CONFLICT(group_id) DO UPDATE SET image_path = excluded.image_path;";
        command.Parameters.AddWithValue("$groupId", groupId);
        command.Parameters.AddWithValue("$imagePath", (object?)imagePath ?? DBNull.Value);
        command.ExecuteNonQuery();

        DataChanged?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
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

        await SaveGroupMetadataAsync(group.Id, normalizedName, DateTimeOffset.UtcNow);
        await AddMembersAsync(group.Id, memberDrafts);
        await SelectGroupAsync(group.Id);
        return group.Id;
    }

    public async Task UpdateGroupAsync(string groupId, string groupName, string currency)
    {
        var normalizedName = NormalizeRequired(groupName, AppResources.Validation_GroupNameRequired);
        var normalizedCurrency = NormalizeRequired(currency, AppResources.Validation_CurrencyRequired).ToUpperInvariant();
        var infra = await GetInfraAsync();
        var existing = await infra.GroupRepository.GetByIdAsync(groupId, CancellationToken.None)
            ?? throw new InvalidOperationException(AppResources.Validation_GroupNotFound);

        await infra.GroupRepository.SaveGroupAsync(existing with { Currency = normalizedCurrency }, CancellationToken.None);

        var metadata = await GetGroupMetadataAsync(groupId);
        await SaveGroupMetadataAsync(groupId, normalizedName, metadata.LastOpenedUtc);
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
        var participants = (await GetOverviewAsync(groupId)).Participants;
        if (participants.Any(participant => string.Equals(participant.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(AppResources.Validation_PersonNameMustBeUnique);
        }

        await AddMembersAsync(groupId, new[] { new GroupDraftMember(normalizedName, normalizedHouseholdName, consumptionCategory, customConsumptionWeight) });
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateGroupMemberAsync(string groupId, string participantId, string personName, string? dependsOnParticipantId)
    {
        var normalizedParticipantId = NormalizeRequired(participantId, AppResources.Validation_PersonNotFound);
        var normalizedName = NormalizeRequired(personName, AppResources.Validation_PersonNameRequired);
        var normalizedDependsOnParticipantId = NormalizeOptional(dependsOnParticipantId);

        var infra = await GetInfraAsync();
        var group = await infra.GroupRepository.GetByIdAsync(groupId, CancellationToken.None)
            ?? throw new InvalidOperationException(AppResources.Validation_GroupNotFound);
        if (group.Closed)
        {
            throw new InvalidOperationException(AppResources.Validation_GroupArchivedReadonly);
        }

        var participants = await infra.ParticipantRepository.ListParticipantsByGroupIdAsync(groupId, CancellationToken.None);
        var participant = participants.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalizedParticipantId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(AppResources.Validation_PersonNotFound);

        if (participants.Any(candidate =>
                !string.Equals(candidate.Id, participant.Id, StringComparison.Ordinal)
                && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(AppResources.Validation_PersonNameMustBeUnique);
        }

        var economicUnits = await infra.EconomicUnitRepository.ListEconomicUnitsByGroupIdAsync(groupId, CancellationToken.None);
        var currentUnit = economicUnits.FirstOrDefault(unit => string.Equals(unit.Id, participant.EconomicUnitId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(AppResources.Validation_PersonNotFound);

        var destinationUnit = currentUnit;
        if (!string.IsNullOrWhiteSpace(normalizedDependsOnParticipantId))
        {
            var responsibleParticipant = participants.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, normalizedDependsOnParticipantId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(AppResources.Validation_ResponsiblePersonNotFound);

            if (string.Equals(responsibleParticipant.Id, participant.Id, StringComparison.Ordinal)
                && !string.Equals(currentUnit.OwnerParticipantId, participant.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AppResources.Validation_PersonCannotDependOnSelf);
            }

            destinationUnit = economicUnits.FirstOrDefault(unit => string.Equals(unit.Id, responsibleParticipant.EconomicUnitId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(AppResources.Validation_ResponsiblePersonNotFound);
        }

        var updatedParticipant = participant with
        {
            Name = normalizedName,
            EconomicUnitId = destinationUnit.Id
        };
        await infra.ParticipantRepository.SaveParticipantAsync(updatedParticipant, CancellationToken.None);
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
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var expenseIdGenerator = new GuidIdGenerator();

        var split = splitDefinition ?? new SplitDefinition(new SplitComponent[]
        {
            new RemainderSplitComponent(participantIds, RemainderMode.Equal)
        });

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            expenseIdGenerator,
            new UtcClock()).ExecuteAsync(new AddExpenseInput(
                GroupId: selectedGroupId,
                Title: title,
                PaidByParticipantId: paidByParticipantId,
                AmountMinor: amountMinor,
                SplitDefinition: split,
                Date: date.ToUniversalTime().ToString("O")));

        if (!string.IsNullOrWhiteSpace(icon))
        {
            await SaveExpenseIconAsync(expenseIdGenerator.LastGeneratedId, icon.Trim());
        }

        _lastPaidByParticipantId = paidByParticipantId;
        _lastParticipantIds = participantIds.ToArray();

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPaymentAsync(string fromParticipantId, string toParticipantId, long amountMinor, DateTime date)
    {
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();

        await new AddManualTransferUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.TransferRepository,
            new GuidIdGenerator(),
            new UtcClock()).ExecuteAsync(new AddManualTransferInput(
                GroupId: selectedGroupId,
                FromParticipantId: fromParticipantId,
                ToParticipantId: toParticipantId,
                AmountMinor: amountMinor,
                Date: date.ToUniversalTime().ToString("O"),
                Note: "Recorded in app"));

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ExpenseModel?> GetExpenseAsync(string expenseId)
    {
        var normalizedExpenseId = expenseId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedExpenseId))
        {
            return null;
        }

        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var expense = await infra.ExpenseRepository.GetExpenseByIdAsync(normalizedExpenseId, CancellationToken.None);
        if (expense is null || !string.Equals(expense.GroupId, selectedGroupId, StringComparison.Ordinal))
        {
            return null;
        }

        return new ExpenseModel(
            expense.Id,
            expense.GroupId,
            expense.Title,
            expense.PaidByParticipantId,
            expense.AmountMinor,
            expense.Date,
            expense.SplitDefinition,
            expense.Notes);
    }

    public async Task UpdateExpenseAsync(
        string expenseId,
        string title,
        string paidByParticipantId,
        long amountMinor,
        DateTime date,
        SplitDefinition splitDefinition,
        string? notes)
    {
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();

        await new EditExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository).ExecuteAsync(new EditExpenseInput(
                GroupId: selectedGroupId,
                ExpenseId: expenseId,
                Title: title,
                PaidByParticipantId: paidByParticipantId,
                AmountMinor: amountMinor,
                SplitDefinition: splitDefinition,
                Date: date.ToUniversalTime().ToString("O"),
                Notes: notes));

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteExpenseAsync(string expenseId)
    {
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();

        await new DeleteExpenseUseCase(
            infra.GroupRepository,
            infra.ExpenseRepository).ExecuteAsync(new DeleteExpenseInput(
                GroupId: selectedGroupId,
                ExpenseId: expenseId));

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<(IReadOnlyList<BalanceModel> Balances, SettlementPlanModel Settlement)> GetSettlementAsync(SettlementMode mode)
    {
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();

        IReadOnlyList<BalanceModel> balances = mode == SettlementMode.Participant
            ? await new GetBalancesByParticipantUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.ExpenseRepository, infra.TransferRepository)
                .ExecuteAsync(selectedGroupId)
            : await new GetBalancesByEconomicUnitOwnerUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository, infra.ExpenseRepository, infra.TransferRepository)
                .ExecuteAsync(selectedGroupId);

        var settlement = await new GetSettlementPlanUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(selectedGroupId, mode);

        return (balances, settlement);
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

        var groupIds = await ListGroupIdsAsync();
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

    private async Task<IReadOnlyList<string>> ListGroupIdsAsync()
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = "SELECT id FROM groups ORDER BY id";

        var groupIds = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            groupIds.Add(reader.GetString(0));
        }

        return groupIds;
    }

    private async Task<GroupMetadataRecord> GetGroupMetadataAsync(string groupId)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = "SELECT name, last_opened_utc, image_path FROM group_metadata WHERE group_id = $groupId";
        command.Parameters.AddWithValue("$groupId", groupId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new GroupMetadataRecord(null, null);
        }

        return new GroupMetadataRecord(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) || !DateTimeOffset.TryParse(reader.GetString(1), out var lastOpenedUtc) ? null : lastOpenedUtc,
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private async Task SaveGroupMetadataAsync(string groupId, string? name, DateTimeOffset? lastOpenedUtc)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO group_metadata (group_id, name, last_opened_utc)
VALUES ($groupId, $name, $lastOpenedUtc)
ON CONFLICT(group_id) DO UPDATE SET
  name = COALESCE(excluded.name, group_metadata.name),
  last_opened_utc = COALESCE(excluded.last_opened_utc, group_metadata.last_opened_utc);";
        command.Parameters.AddWithValue("$groupId", groupId);
        command.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastOpenedUtc", lastOpenedUtc?.ToString("O") ?? (object)DBNull.Value);
        command.ExecuteNonQuery();

        await Task.CompletedTask;
    }

    private Task TouchGroupAsync(string groupId)
        => SaveGroupMetadataAsync(groupId, null, DateTimeOffset.UtcNow);

    private async Task<IReadOnlyDictionary<string, string>> GetExpenseIconsAsync(string groupId)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
SELECT expenses.id, expense_ui_metadata.icon
FROM expenses
INNER JOIN expense_ui_metadata ON expense_ui_metadata.expense_id = expenses.id
WHERE expenses.group_id = $groupId AND expense_ui_metadata.icon IS NOT NULL;";
        command.Parameters.AddWithValue("$groupId", groupId);

        var icons = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            icons[reader.GetString(0)] = reader.GetString(1);
        }

        return icons;
    }

    private async Task SaveExpenseIconAsync(string expenseId, string icon)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO expense_ui_metadata (expense_id, icon)
VALUES ($expenseId, $icon)
ON CONFLICT(expense_id) DO UPDATE SET
  icon = excluded.icon;";
        command.Parameters.AddWithValue("$expenseId", expenseId);
        command.Parameters.AddWithValue("$icon", icon);
        command.ExecuteNonQuery();

        await Task.CompletedTask;
    }

    private async Task AddMembersAsync(string groupId, IReadOnlyList<GroupDraftMember> members)
    {
        var infra = await GetInfraAsync();
        var createEconomicUnit = new CreateEconomicUnitUseCase(infra.GroupRepository, infra.EconomicUnitRepository, _idGenerator);
        var createParticipant = new CreateParticipantUseCase(infra.GroupRepository, infra.EconomicUnitRepository, infra.ParticipantRepository, _idGenerator);
        var overview = await GetOverviewAsync(groupId);
        var unitsByName = overview.EconomicUnits
            .Where(unit => !string.IsNullOrWhiteSpace(unit.Name))
            .GroupBy(unit => unit.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
        {
            var householdName = NormalizeOptional(member.HouseholdName);
            EconomicUnitModel unit;

            if (!string.IsNullOrWhiteSpace(householdName) && unitsByName.TryGetValue(householdName, out var existingUnit))
            {
                unit = existingUnit;
            }
            else
            {
                unit = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(
                    groupId,
                    _idGenerator.NextId(),
                    householdName));

                if (!string.IsNullOrWhiteSpace(householdName))
                {
                    unitsByName[householdName] = unit;
                }
            }

            await createParticipant.ExecuteAsync(new CreateParticipantInput(
                groupId,
                unit.Id,
                member.Name,
                member.ConsumptionCategory,
                member.CustomConsumptionWeight));
        }
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

    private sealed class GuidIdGenerator : LuSplit.Application.Ports.IIdGenerator
    {
        public string LastGeneratedId { get; private set; } = string.Empty;

        public string NextId()
        {
            LastGeneratedId = Guid.NewGuid().ToString("N")[..12];
            return LastGeneratedId;
        }
    }

    private sealed class UtcClock : LuSplit.Application.Ports.IClock
    {
        public string NowIso() => DateTimeOffset.UtcNow.ToString("O");
    }
}

public sealed record EventDraftDefaults(string? PaidByParticipantId, IReadOnlyList<string> ParticipantIds);
public sealed record GroupWorkspaceModel(
    string GroupId,
    string GroupName,
    GroupOverviewModel Overview,
    IReadOnlyDictionary<string, string> ExpenseIcons,
    DateTimeOffset? LastOpenedUtc,
    string? ImagePath = null);

public sealed record GroupListItemModel(
    string GroupId,
    string Name,
    string Currency,
    bool IsCurrent,
    string SummaryText,
    string BalancePreviewText,
    string StatusText,
    DateTimeOffset RankDate,
    string? ImagePath = null)
{
    public string AvatarInitial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public sealed record GroupDetailsModel(
    string GroupId,
    string GroupName,
    string Currency,
    bool IsArchived,
    IReadOnlyList<GroupMemberModel> Members,
    string? ImagePath = null);

public sealed record GroupMemberModel(
    string ParticipantId,
    string Name,
    string HouseholdName,
    bool IsOwner,
    string ConsumptionCategory,
    string? CustomConsumptionWeight);

public sealed record GroupDraftMember(
    string Name,
    string? HouseholdName,
    ConsumptionCategory ConsumptionCategory = ConsumptionCategory.Full,
    string? CustomConsumptionWeight = null);

sealed record GroupMetadataRecord(string? Name, DateTimeOffset? LastOpenedUtc, string? ImagePath = null);
