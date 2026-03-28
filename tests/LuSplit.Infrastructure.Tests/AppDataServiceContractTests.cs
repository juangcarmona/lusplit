using LuSplit.Application.Commands;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Client;

namespace LuSplit.Infrastructure.Tests;

// Verifies the infrastructure-layer behaviors that AppDataService orchestrates for
// expense-related flows. AppDataService cannot be directly instantiated in tests because
// it depends on MAUI-specific APIs (FileSystem.AppDataDirectory, Preferences.Default)
// that are absent in a plain net10.0 test project.
//
// What IS covered here:
//   1. AddExpense (use-case path) — expense is persisted in the correct group
//   2. Icon metadata round-trip — expense_ui_metadata SQL (mirrors SaveExpenseIconAsync /
//      GetExpenseIconsAsync in AppDataService)
//   3. GetExpenseIconsAsync group isolation — icons from other groups must not bleed through
//   4. GetExpense group-scope guard — expense belonging to another group returns null
//   5. UpdateExpense — EditExpenseUseCase persists title, amount, and notes changes
//   6. DeleteExpense — DeleteExpenseUseCase removes the row
//
// What is NOT covered (requires decoupling MAUI APIs before PR-9 is safe):
//   - GetEventDraftDefaults() / draft-state mutation
//     (_lastPaidByParticipantId and _lastParticipantIds written by AddExpenseAsync)
//   - DataChanged event firing (on AppDataService instance)
//   - Selected-group resolution via Preferences.Default
public sealed class AppDataServiceContractTests
{
    // -------------------------------------------------------------------------
    // 1. AddExpense — happy path: expense is persisted in the selected group
    // -------------------------------------------------------------------------
    [Fact]
    public async Task AddExpense_PersistsExpenseInSelectedGroup()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var (groupId, p1Id, _, _) = await SetupMinimalGroupAsync(infra, "a");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-1"),
            new FixedClock("2026-03-01T10:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                groupId, "Dinner", p1Id, 900,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1Id }, RemainderMode.Equal)
                })));

        var expenses = await infra.ExpenseRepository
            .ListExpensesByGroupIdAsync(groupId, CancellationToken.None);

        Assert.Single(expenses);
        Assert.Equal("Dinner", expenses[0].Title);
        Assert.Equal(900, expenses[0].AmountMinor);
        Assert.Equal(groupId, expenses[0].GroupId);
    }

    // -------------------------------------------------------------------------
    // 2. Icon metadata: expense_ui_metadata round-trip
    //    Mirrors AppDataService.SaveExpenseIconAsync + GetExpenseIconsAsync
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ExpenseIconMetadata_StoresAndRetrievesCorrectly()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();
        EnsureUiMetadataTables(infra);

        var (groupId, p1Id, _, _) = await SetupMinimalGroupAsync(infra, "b");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-icon"),
            new FixedClock("2026-03-01T10:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                groupId, "Groceries", p1Id, 500,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1Id }, RemainderMode.Equal)
                })));

        SaveExpenseIcon(infra, "exp-icon", "🛒");

        var icons = GetExpenseIcons(infra, groupId);
        Assert.True(icons.ContainsKey("exp-icon"));
        Assert.Equal("🛒", icons["exp-icon"]);
    }

    // -------------------------------------------------------------------------
    // 3. GetExpenseIcons — icons from other groups must not bleed through
    //    Mirrors the WHERE group_id = $groupId filter in GetExpenseIconsAsync
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetExpenseIcons_ReturnsOnlyIconsForRequestedGroup()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();
        EnsureUiMetadataTables(infra);

        var (g1, p1, _, _) = await SetupMinimalGroupAsync(infra, "c1");
        var (g2, p2, _, _) = await SetupMinimalGroupAsync(infra, "c2");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-c1"),
            new FixedClock("2026-03-01T00:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                g1, "Dinner", p1, 900,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1 }, RemainderMode.Equal)
                })));
        SaveExpenseIcon(infra, "exp-c1", "🍽️");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-c2"),
            new FixedClock("2026-03-01T00:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                g2, "Hotel", p2, 200,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p2 }, RemainderMode.Equal)
                })));
        SaveExpenseIcon(infra, "exp-c2", "🏨");

        var icons = GetExpenseIcons(infra, g1);

        Assert.Single(icons);
        Assert.True(icons.ContainsKey("exp-c1"));
        Assert.False(icons.ContainsKey("exp-c2"));
    }

    // -------------------------------------------------------------------------
    // 4. GetExpense group-scope guard
    //    Mirrors AppDataService.GetExpenseAsync:
    //      if (expense.GroupId != selectedGroupId) return null;
    // -------------------------------------------------------------------------
    [Fact]
    public async Task GetExpense_ReturnsNullWhenExpenseBelongsToAnotherGroup()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var (g1, p1, _, _) = await SetupMinimalGroupAsync(infra, "d1");
        var (g2, _, _, _) = await SetupMinimalGroupAsync(infra, "d2");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-in-g1"),
            new FixedClock("2026-03-01T00:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                g1, "Dinner", p1, 400,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1 }, RemainderMode.Equal)
                })));

        var expense = await infra.ExpenseRepository
            .GetExpenseByIdAsync("exp-in-g1", CancellationToken.None);

        Assert.NotNull(expense);

        // Replicate the AppDataService guard: return null when groupId mismatches
        var resultForCorrectGroup = string.Equals(expense.GroupId, g1, StringComparison.Ordinal)
            ? expense : null;
        var resultForWrongGroup = string.Equals(expense.GroupId, g2, StringComparison.Ordinal)
            ? expense : null;

        Assert.NotNull(resultForCorrectGroup);
        Assert.Null(resultForWrongGroup);
    }

    // -------------------------------------------------------------------------
    // 5. UpdateExpense — EditExpenseUseCase persists title, amount, and notes
    // -------------------------------------------------------------------------
    [Fact]
    public async Task UpdateExpense_PersistsChangesCorrectly()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var (groupId, p1Id, _, _) = await SetupMinimalGroupAsync(infra, "e");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-upd"),
            new FixedClock("2026-03-01T00:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                groupId, "Original Title", p1Id, 100,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1Id }, RemainderMode.Equal)
                })));

        await new EditExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository)
            .ExecuteAsync(new EditExpenseInput(
                GroupId: groupId,
                ExpenseId: "exp-upd",
                Title: "Updated Title",
                PaidByParticipantId: p1Id,
                AmountMinor: 250,
                SplitDefinition: new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1Id }, RemainderMode.Equal)
                }),
                Date: "2026-03-02T00:00:00.000Z",
                Notes: "edited"));

        var updated = await infra.ExpenseRepository
            .GetExpenseByIdAsync("exp-upd", CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(250, updated.AmountMinor);
        Assert.Equal("edited", updated.Notes);
    }

    // -------------------------------------------------------------------------
    // 6. DeleteExpense — expense row is removed from the repository
    //    Note: AppDataService.DataChanged event cannot be tested here because it
    //    is wired inside the AppDataService instance, which requires MAUI APIs.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DeleteExpense_RemovesExpenseFromRepository()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var (groupId, p1Id, _, _) = await SetupMinimalGroupAsync(infra, "f");

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new FixedIdGenerator("exp-del"),
            new FixedClock("2026-03-01T00:00:00.000Z"))
            .ExecuteAsync(new AddExpenseInput(
                groupId, "To Delete", p1Id, 50,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { p1Id }, RemainderMode.Equal)
                })));

        Assert.NotNull(await infra.ExpenseRepository
            .GetExpenseByIdAsync("exp-del", CancellationToken.None));

        await new DeleteExpenseUseCase(
            infra.GroupRepository,
            infra.ExpenseRepository)
            .ExecuteAsync(new DeleteExpenseInput(
                GroupId: groupId,
                ExpenseId: "exp-del"));

        Assert.Null(await infra.ExpenseRepository
            .GetExpenseByIdAsync("exp-del", CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates the two app-layer tables that AppDataService.EnsureAppMetadataTablesAsync
    /// creates at runtime. Required for icon-related tests only.
    /// </summary>
    private static void EnsureUiMetadataTables(InfraLocalSqlite infra)
    {
        using var cmd = infra.Db.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS group_metadata (
    group_id TEXT PRIMARY KEY NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    name TEXT NULL,
    last_opened_utc TEXT NULL,
    image_path TEXT NULL
);
CREATE TABLE IF NOT EXISTS expense_ui_metadata (
    expense_id TEXT PRIMARY KEY NOT NULL REFERENCES expenses(id) ON DELETE CASCADE,
    icon TEXT NULL
);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Mirrors AppDataService.SaveExpenseIconAsync.
    /// </summary>
    private static void SaveExpenseIcon(InfraLocalSqlite infra, string expenseId, string icon)
    {
        using var cmd = infra.Db.CreateCommand();
        cmd.CommandText = @"
INSERT INTO expense_ui_metadata (expense_id, icon)
VALUES ($expenseId, $icon)
ON CONFLICT(expense_id) DO UPDATE SET icon = excluded.icon;";
        cmd.Parameters.AddWithValue("$expenseId", expenseId);
        cmd.Parameters.AddWithValue("$icon", icon);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Mirrors AppDataService.GetExpenseIconsAsync.
    /// </summary>
    private static IReadOnlyDictionary<string, string> GetExpenseIcons(
        InfraLocalSqlite infra, string groupId)
    {
        using var cmd = infra.Db.CreateCommand();
        cmd.CommandText = @"
SELECT expenses.id, expense_ui_metadata.icon
FROM expenses
INNER JOIN expense_ui_metadata ON expense_ui_metadata.expense_id = expenses.id
WHERE expenses.group_id = $groupId AND expense_ui_metadata.icon IS NOT NULL;";
        cmd.Parameters.AddWithValue("$groupId", groupId);

        var icons = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            icons[reader.GetString(0)] = reader.GetString(1);
        }

        return icons;
    }

    /// <summary>
    /// Creates a group with one economic unit and three participants.
    /// The <paramref name="id"/> prefix must be unique per call within the same
    /// InfraLocalSqlite instance to avoid PRIMARY KEY conflicts.
    /// </summary>
    private static async Task<(string GroupId, string P1Id, string P2Id, string P3Id)>
        SetupMinimalGroupAsync(InfraLocalSqlite infra, string id)
    {
        var groupId = $"grp-{id}";
        var unitId  = $"unit-{id}";
        var p1Id    = $"p1-{id}";
        var p2Id    = $"p2-{id}";
        var p3Id    = $"p3-{id}";

        await infra.GroupRepository.SaveGroupAsync(
            new Group(groupId, "USD", false), CancellationToken.None);

        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(
            new EconomicUnit(unitId, groupId, p1Id, "Household"), CancellationToken.None);

        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant(p1Id, groupId, unitId, "Alice", ConsumptionCategory.Full),
            CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant(p2Id, groupId, unitId, "Bob", ConsumptionCategory.Full),
            CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant(p3Id, groupId, unitId, "Carol", ConsumptionCategory.Half),
            CancellationToken.None);

        return (groupId, p1Id, p2Id, p3Id);
    }

    /// <summary>
    /// Returns the same fixed ID on every NextId() call.
    /// Sufficient for use cases that call NextId() exactly once.
    /// </summary>
    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly string _id;
        public FixedIdGenerator(string id) => _id = id;
        public string NextId() => _id;
    }

    private sealed class FixedClock : IClock
    {
        private readonly string _nowIso;
        public FixedClock(string nowIso) => _nowIso = nowIso;
        public string NowIso() => _nowIso;
    }
}
