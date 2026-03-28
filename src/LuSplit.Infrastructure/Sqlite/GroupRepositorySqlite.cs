using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public sealed class GroupRepositorySqlite : IGroupRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public GroupRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, currency, closed FROM groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", groupId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<Group?>(null);
        }

        var group = new Group(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2) == 1);

        return Task.FromResult<Group?>(group);
    }

    public Task SaveGroupAsync(Group group, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO groups (id, currency, closed)
VALUES ($id, $currency, $closed)
ON CONFLICT(id) DO UPDATE SET
  currency = excluded.currency,
  closed = excluded.closed";
            command.Parameters.AddWithValue("$id", group.Id);
            command.Parameters.AddWithValue("$currency", group.Currency);
            command.Parameters.AddWithValue("$closed", group.Closed ? 1 : 0);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
