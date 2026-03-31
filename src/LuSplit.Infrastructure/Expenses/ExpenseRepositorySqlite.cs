using LuSplit.Application.Expenses.Ports;
using LuSplit.Domain.Expenses;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Expenses;

public sealed class ExpenseRepositorySqlite : IExpenseRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public ExpenseRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
FROM expenses
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var expenses = new List<Expense>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                SplitJson.ParseDefinition(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return Task.FromResult<IReadOnlyList<Expense>>(expenses);
    }

    public Task<Expense?> GetExpenseByIdAsync(string expenseId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
FROM expenses
WHERE id = $id";
        command.Parameters.AddWithValue("$id", expenseId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<Expense?>(null);
        }

        return Task.FromResult<Expense?>(new Expense(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetString(5),
            SplitJson.ParseDefinition(reader.GetString(6)),
            reader.IsDBNull(7) ? null : reader.GetString(7)));
    }

    public Task SaveAsync(Expense expense, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "expenses", expense.Id, expense.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO expenses (
  group_id, id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
) VALUES ($groupId, $id, $title, $paidBy, $amountMinor, $date, $split, $notes)
ON CONFLICT(id) DO UPDATE SET
  title = excluded.title,
  paid_by_participant_id = excluded.paid_by_participant_id,
  amount_minor = excluded.amount_minor,
  date = excluded.date,
  split_definition_json = excluded.split_definition_json,
  notes = excluded.notes";
            command.Parameters.AddWithValue("$groupId", expense.GroupId);
            command.Parameters.AddWithValue("$id", expense.Id);
            command.Parameters.AddWithValue("$title", expense.Title);
            command.Parameters.AddWithValue("$paidBy", expense.PaidByParticipantId);
            command.Parameters.AddWithValue("$amountMinor", expense.AmountMinor);
            command.Parameters.AddWithValue("$date", expense.Date);
            command.Parameters.AddWithValue("$split", SplitJson.SerializeDefinition(expense.SplitDefinition));
            command.Parameters.AddWithValue("$notes", (object?)expense.Notes ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    public Task DeleteAsync(string groupId, string expenseId, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM expenses WHERE group_id = $groupId AND id = $id";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$id", expenseId);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
