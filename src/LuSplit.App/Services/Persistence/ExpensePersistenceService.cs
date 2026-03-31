using LuSplit.Application.Expenses.Commands;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Expenses;
using LuSplit.Infrastructure.Sqlite;

namespace LuSplit.App.Services.Persistence;

/// <summary>
/// Handles all expense-related persistence: add, read, update, delete, and
/// the UI-layer icon metadata table. Requires a delegate to resolve the
/// initialized <see cref="InfraLocalSqlite"/> instance and a delegate to
/// resolve the currently selected group ID, both supplied by
/// <see cref="AppDataService"/> at construction time.
/// </summary>
internal sealed class ExpensePersistenceService
{
    private readonly Func<Task<InfraLocalSqlite>> _getInfra;
    private readonly Func<Task<string>> _getSelectedGroupId;

    internal ExpensePersistenceService(
        Func<Task<InfraLocalSqlite>> getInfra,
        Func<Task<string>> getSelectedGroupId)
    {
        _getInfra = getInfra;
        _getSelectedGroupId = getSelectedGroupId;
    }

    internal async Task AddExpenseAsync(
        string title,
        long amountMinor,
        string paidByParticipantId,
        DateTime date,
        IReadOnlyList<string> participantIds,
        string? icon,
        SplitDefinition? splitDefinition = null)
    {
        var infra = await _getInfra();
        var selectedGroupId = await _getSelectedGroupId();
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
            await SaveExpenseIconAsync(infra, expenseIdGenerator.LastGeneratedId, icon.Trim());
        }
    }

    internal async Task<ExpenseModel?> GetExpenseAsync(string expenseId)
    {
        var normalizedExpenseId = expenseId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedExpenseId))
        {
            return null;
        }

        var infra = await _getInfra();
        var selectedGroupId = await _getSelectedGroupId();
        var expense = await infra.ExpenseRepository.GetExpenseByIdAsync(normalizedExpenseId, CancellationToken.None);
        if (expense is null || !string.Equals(expense.GroupId, selectedGroupId, StringComparison.Ordinal))
        {
            return null;
        }

        return MapExpense(expense);
    }

    internal async Task<ExpenseModel?> GetExpenseAsync(string expenseId, string groupId)
    {
        var normalizedExpenseId = expenseId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedExpenseId)) return null;

        var infra = await _getInfra();
        var expense = await infra.ExpenseRepository.GetExpenseByIdAsync(normalizedExpenseId, CancellationToken.None);
        if (expense is null || !string.Equals(expense.GroupId, groupId, StringComparison.Ordinal))
            return null;

        return MapExpense(expense);
    }

    private static ExpenseModel MapExpense(Expense expense) =>
        new(expense.Id, expense.GroupId, expense.Title, expense.PaidByParticipantId,
            expense.AmountMinor, expense.Date, expense.SplitDefinition, expense.Notes);

    internal async Task UpdateExpenseAsync(
        string expenseId,
        string title,
        string paidByParticipantId,
        long amountMinor,
        DateTime date,
        SplitDefinition splitDefinition,
        string? notes)
    {
        var infra = await _getInfra();
        var selectedGroupId = await _getSelectedGroupId();

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
    }

    internal async Task DeleteExpenseAsync(string expenseId)
    {
        var infra = await _getInfra();
        var selectedGroupId = await _getSelectedGroupId();

        await new DeleteExpenseUseCase(
            infra.GroupRepository,
            infra.ExpenseRepository).ExecuteAsync(new DeleteExpenseInput(
                GroupId: selectedGroupId,
                ExpenseId: expenseId));
    }

    internal async Task<IReadOnlyDictionary<string, string>> GetExpenseIconsAsync(string groupId)
    {
        var infra = await _getInfra();

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

    private static async Task SaveExpenseIconAsync(InfraLocalSqlite infra, string expenseId, string icon)
    {
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
}

internal sealed class GuidIdGenerator : IIdGenerator
{
    public string LastGeneratedId { get; private set; } = string.Empty;

    public string NextId()
    {
        LastGeneratedId = Guid.NewGuid().ToString("N")[..12];
        return LastGeneratedId;
    }
}

internal sealed class UtcClock : IClock
{
    public string NowIso() => DateTimeOffset.UtcNow.ToString("O");
}
