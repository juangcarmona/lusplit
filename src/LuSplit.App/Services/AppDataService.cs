using LuSplit.Application.Commands;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Client;
using LuSplit.Infrastructure.Export;
using Microsoft.Maui.Storage;

namespace LuSplit.App.Services;

public sealed class AppDataService : IAsyncDisposable
{
    private const string DefaultGroupId = "g1";
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
            await EnsureSeedDataAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GroupOverviewModel> GetOverviewAsync()
        => (await GetTripWorkspaceAsync()).Overview;

    public async Task<GroupOverviewModel> GetOverviewAsync(string groupId)
        => (await GetTripWorkspaceAsync(groupId)).Overview;

    public async Task<TripWorkspaceModel> GetTripWorkspaceAsync()
        => await GetTripWorkspaceAsync(await GetSelectedGroupIdAsync());

    public async Task<TripWorkspaceModel> GetTripWorkspaceAsync(string groupId)
    {
        var infra = await GetInfraAsync();
        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(groupId);

        var metadata = await GetTripMetadataAsync(groupId);
        return new TripWorkspaceModel(
            groupId,
            ResolveTripName(metadata.Name, overview),
            overview,
            await GetExpenseIconsAsync(groupId),
            metadata.LastOpenedUtc);
    }

    public async Task<IReadOnlyList<ParticipantModel>> GetParticipantsAsync()
    {
        var overview = await GetOverviewAsync();
        return overview.Participants;
    }

    public async Task<IReadOnlyList<TripListItemModel>> GetTripsAsync()
    {
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var groupIds = await ListGroupIdsAsync();
        var trips = new List<TripListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var workspace = await GetTripWorkspaceAsync(groupId);
            // Archived trips are hidden from the active list.
            if (workspace.Overview.Group.Closed) continue;
            var lastActivity = GetLastActivity(workspace.Overview);
            var rankDate = workspace.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            trips.Add(new TripListItemModel(
                groupId,
                workspace.TripName,
                workspace.Overview.Group.Currency,
                string.Equals(groupId, selectedGroupId, StringComparison.Ordinal),
                TripPresentationMapper.BuildTripSummary(workspace.Overview),
                TripPresentationMapper.BuildBalancePreview(workspace.Overview, 1).FirstOrDefault() ?? "Add the first event.",
                BuildTripStatusText(string.Equals(groupId, selectedGroupId, StringComparison.Ordinal), workspace.LastOpenedUtc, lastActivity),
                rankDate));
        }

        return trips
            .OrderByDescending(trip => trip.IsCurrent)
            .ThenByDescending(trip => trip.RankDate)
            .ThenBy(trip => trip.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<TripListItemModel>> GetArchivedTripsAsync()
    {
        var groupIds = await ListGroupIdsAsync();
        var trips = new List<TripListItemModel>(groupIds.Count);

        foreach (var groupId in groupIds)
        {
            var workspace = await GetTripWorkspaceAsync(groupId);
            if (!workspace.Overview.Group.Closed) continue;
            var lastActivity = GetLastActivity(workspace.Overview);
            var rankDate = workspace.LastOpenedUtc ?? lastActivity ?? DateTimeOffset.MinValue;
            trips.Add(new TripListItemModel(
                groupId,
                workspace.TripName,
                workspace.Overview.Group.Currency,
                false,
                TripPresentationMapper.BuildTripSummary(workspace.Overview),
                TripPresentationMapper.BuildBalancePreview(workspace.Overview, 1).FirstOrDefault() ?? "Settled.",
                string.Empty,
                rankDate));
        }

        return trips
            .OrderByDescending(t => t.RankDate)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<TripDetailsModel> GetTripDetailsAsync()
        => await GetTripDetailsAsync(await GetSelectedGroupIdAsync());

    public async Task<TripDetailsModel> GetTripDetailsAsync(string groupId)
    {
        var workspace = await GetTripWorkspaceAsync(groupId);
        var householdNames = BuildHouseholdLookup(workspace.Overview);
        var ownerIdsByUnit = workspace.Overview.EconomicUnits
            .ToDictionary(unit => unit.Id, unit => unit.OwnerParticipantId, StringComparer.Ordinal);

        return new TripDetailsModel(
            groupId,
            workspace.TripName,
            workspace.Overview.Group.Currency,
            workspace.Overview.Group.Closed,
            workspace.Overview.Participants
                .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
                .Select(participant => new TripMemberModel(
                    participant.Id,
                    participant.Name,
                    householdNames.TryGetValue(participant.EconomicUnitId, out var householdName) ? householdName : participant.Name,
                    ownerIdsByUnit.TryGetValue(participant.EconomicUnitId, out var ownerId) &&
                        string.Equals(ownerId, participant.Id, StringComparison.Ordinal),
                    participant.ConsumptionCategory.ToString().ToUpperInvariant(),
                    participant.CustomConsumptionWeight))
                .ToArray());
    }

    /// <summary>Archives a trip. Archived trips are read-only — the domain blocks new expenses and participants on closed groups.</summary>
    public async Task ArchiveTripAsync(string groupId)
    {
        var infra = await GetInfraAsync();
        await new CloseGroupUseCase(infra.GroupRepository).ExecuteAsync(new CloseGroupInput(groupId));

        // If the archived trip was the active selection, fall through to the next active trip.
        if (string.Equals(_selectedGroupId, groupId, StringComparison.Ordinal))
        {
            _selectedGroupId = null;
            Preferences.Default.Remove(SelectedGroupPreferenceKey);
        }

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public EventDraftDefaults GetEventDraftDefaults()
        => new(_lastPaidByParticipantId, _lastParticipantIds);

    public async Task SelectTripAsync(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        _selectedGroupId = groupId.Trim();
        Preferences.Default.Set(SelectedGroupPreferenceKey, _selectedGroupId);
        await TouchTripAsync(_selectedGroupId);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string> CreateTripAsync(string tripName, string currency, IReadOnlyList<TripDraftMember> members)
    {
        var normalizedName = NormalizeRequired(tripName, "Trip name is required.");
        var normalizedCurrency = NormalizeRequired(currency, "Currency is required.").ToUpperInvariant();

        var memberDrafts = members
            .Select(member => new TripDraftMember(
                NormalizeRequired(member.Name, "Each person needs a name."),
                NormalizeOptional(member.HouseholdName),
                member.ConsumptionCategory,
                member.CustomConsumptionWeight))
            .ToArray();

        if (memberDrafts.Length == 0)
        {
            throw new InvalidOperationException("Add at least one person.");
        }

        var infra = await GetInfraAsync();
        var group = await new CreateGroupUseCase(infra.GroupRepository, _idGenerator)
            .ExecuteAsync(new CreateGroupInput(normalizedCurrency));

        await SaveTripMetadataAsync(group.Id, normalizedName, DateTimeOffset.UtcNow);
        await AddMembersAsync(group.Id, memberDrafts);
        await SelectTripAsync(group.Id);
        return group.Id;
    }

    public async Task UpdateTripAsync(string groupId, string tripName, string currency)
    {
        var normalizedName = NormalizeRequired(tripName, "Trip name is required.");
        var normalizedCurrency = NormalizeRequired(currency, "Currency is required.").ToUpperInvariant();
        var infra = await GetInfraAsync();
        var existing = await infra.GroupRepository.GetByIdAsync(groupId, CancellationToken.None)
            ?? throw new InvalidOperationException("Trip not found.");

        await infra.GroupRepository.SaveGroupAsync(existing with { Currency = normalizedCurrency }, CancellationToken.None);

        var metadata = await GetTripMetadataAsync(groupId);
        await SaveTripMetadataAsync(groupId, normalizedName, metadata.LastOpenedUtc);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddTripMemberAsync(
        string groupId,
        string personName,
        string? householdName,
        ConsumptionCategory consumptionCategory = ConsumptionCategory.Full,
        string? customConsumptionWeight = null)
    {
        var normalizedName = NormalizeRequired(personName, "Person name is required.");
        var normalizedHouseholdName = NormalizeOptional(householdName);
        await AddMembersAsync(groupId, new[] { new TripDraftMember(normalizedName, normalizedHouseholdName, consumptionCategory, customConsumptionWeight) });
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddExpenseAsync(string title, long amountMinor, string paidByParticipantId, DateTime date, IReadOnlyList<string> participantIds, string? icon)
    {
        var infra = await GetInfraAsync();
        var selectedGroupId = await GetSelectedGroupIdAsync();
        var expenseIdGenerator = new GuidIdGenerator();

        var split = new SplitDefinition(new SplitComponent[]
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

    public async Task<ExportFileResult> ExportTripAsync(string groupId, ExportFormat format)
    {
        var workspace = await GetTripWorkspaceAsync(groupId);
        var outputDir = FileSystem.CacheDirectory;
        var dto = new ExportTripDto(
            groupId,
            workspace.TripName,
            DateTimeOffset.UtcNow.ToString("O"),
            workspace.Overview,
            outputDir);

        var exporter = new TripExporterService();
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
CREATE TABLE IF NOT EXISTS trip_metadata (
  group_id TEXT PRIMARY KEY NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
  name TEXT NULL,
  last_opened_utc TEXT NULL
);

CREATE TABLE IF NOT EXISTS expense_ui_metadata (
  expense_id TEXT PRIMARY KEY NOT NULL REFERENCES expenses(id) ON DELETE CASCADE,
  icon TEXT NULL
);";
        command.ExecuteNonQuery();
        await Task.CompletedTask;
    }

    private async Task EnsureSeedDataAsync()
    {
        var infra = _infra ?? throw new InvalidOperationException("Infrastructure not initialized.");
        var existing = await infra.GroupRepository.GetByIdAsync(DefaultGroupId, CancellationToken.None);
        if (existing is not null)
        {
            return;
        }

        await infra.GroupRepository.SaveGroupAsync(new Group(DefaultGroupId, "USD", false), CancellationToken.None);

        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", DefaultGroupId, "p1", "Household A"), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", DefaultGroupId, "p2", "Household B"), CancellationToken.None);

        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p1", DefaultGroupId, "u1", "Alex", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p2", DefaultGroupId, "u2", "Blair", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p3", DefaultGroupId, "u2", "Casey", ConsumptionCategory.Half), CancellationToken.None);

        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1",
            DefaultGroupId,
            "Dinner",
            "p1",
            900,
            DateTimeOffset.UtcNow.AddDays(-2).ToString("O"),
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2", "p3" }, RemainderMode.Equal)
            }),
            null), CancellationToken.None);

        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e2",
            DefaultGroupId,
            "Groceries",
            "p2",
            600,
            DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
            new SplitDefinition(new SplitComponent[]
            {
                new FixedSplitComponent(new Dictionary<string, long> { ["p1"] = 100 }),
                new RemainderSplitComponent(new[] { "p2", "p3" }, RemainderMode.Equal)
            }),
            "Weekly staples"), CancellationToken.None);

        await SaveTripMetadataAsync(DefaultGroupId, "Weekend trip", DateTimeOffset.UtcNow);
    }

    private async Task<string> GetSelectedGroupIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_selectedGroupId))
        {
            return _selectedGroupId;
        }

        var groupIds = await ListGroupIdsAsync();
        var preferredGroupId = Preferences.Default.Get(SelectedGroupPreferenceKey, DefaultGroupId);
        _selectedGroupId = groupIds.FirstOrDefault(groupId => string.Equals(groupId, preferredGroupId, StringComparison.Ordinal))
            ?? groupIds.FirstOrDefault()
            ?? DefaultGroupId;

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

    private async Task<TripMetadataRecord> GetTripMetadataAsync(string groupId)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = "SELECT name, last_opened_utc FROM trip_metadata WHERE group_id = $groupId";
        command.Parameters.AddWithValue("$groupId", groupId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new TripMetadataRecord(null, null);
        }

        return new TripMetadataRecord(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) || !DateTimeOffset.TryParse(reader.GetString(1), out var lastOpenedUtc) ? null : lastOpenedUtc);
    }

    private async Task SaveTripMetadataAsync(string groupId, string? name, DateTimeOffset? lastOpenedUtc)
    {
        var infra = await GetInfraAsync();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO trip_metadata (group_id, name, last_opened_utc)
VALUES ($groupId, $name, $lastOpenedUtc)
ON CONFLICT(group_id) DO UPDATE SET
  name = COALESCE(excluded.name, trip_metadata.name),
  last_opened_utc = COALESCE(excluded.last_opened_utc, trip_metadata.last_opened_utc);";
        command.Parameters.AddWithValue("$groupId", groupId);
        command.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastOpenedUtc", lastOpenedUtc?.ToString("O") ?? (object)DBNull.Value);
        command.ExecuteNonQuery();

        await Task.CompletedTask;
    }

    private Task TouchTripAsync(string groupId)
        => SaveTripMetadataAsync(groupId, null, DateTimeOffset.UtcNow);

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

    private async Task AddMembersAsync(string groupId, IReadOnlyList<TripDraftMember> members)
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
                    : "Household",
            StringComparer.Ordinal);
    }

    private static string ResolveTripName(string? storedName, GroupOverviewModel overview)
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
            0 => "Trip",
            1 => participantNames[0],
            _ => string.Join(", ", participantNames.Take(participantNames.Length - 1)) + " and " + participantNames[^1]
        };
    }

    private static string BuildTripStatusText(bool isCurrent, DateTimeOffset? lastOpenedUtc, DateTimeOffset? lastActivityUtc)
    {
        if (isCurrent)
        {
            return "Current trip";
        }

        if (lastOpenedUtc.HasValue)
        {
            return $"Opened {TripPresentationMapper.DescribeDay(lastOpenedUtc.Value)}";
        }

        if (lastActivityUtc.HasValue)
        {
            return $"Recent activity {TripPresentationMapper.DescribeDay(lastActivityUtc.Value)}";
        }

        return "Ready for the first event";
    }

    private static DateTimeOffset? GetLastActivity(GroupOverviewModel overview)
    {
        var lastExpense = overview.Expenses.Select(expense => TripPresentationMapper.ParseDate(expense.Date));
        var lastTransfer = overview.Transfers.Select(transfer => TripPresentationMapper.ParseDate(transfer.Date));
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
public sealed record TripWorkspaceModel(
    string GroupId,
    string TripName,
    GroupOverviewModel Overview,
    IReadOnlyDictionary<string, string> ExpenseIcons,
    DateTimeOffset? LastOpenedUtc);

public sealed record TripListItemModel(
    string GroupId,
    string Name,
    string Currency,
    bool IsCurrent,
    string SummaryText,
    string BalancePreviewText,
    string StatusText,
    DateTimeOffset RankDate);

public sealed record TripDetailsModel(
    string GroupId,
    string TripName,
    string Currency,
    bool IsArchived,
    IReadOnlyList<TripMemberModel> Members);

public sealed record TripMemberModel(
    string ParticipantId,
    string Name,
    string HouseholdName,
    bool IsOwner,
    string ConsumptionCategory,
    string? CustomConsumptionWeight);

public sealed record TripDraftMember(
    string Name,
    string? HouseholdName,
    ConsumptionCategory ConsumptionCategory = ConsumptionCategory.Full,
    string? CustomConsumptionWeight = null);

sealed record TripMetadataRecord(string? Name, DateTimeOffset? LastOpenedUtc);
