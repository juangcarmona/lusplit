using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public sealed class EconomicUnitRepositorySqlite : IEconomicUnitRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public EconomicUnitRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, owner_participant_id, name
FROM economic_units
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var units = new List<EconomicUnit>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            units.Add(new EconomicUnit(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return Task.FromResult<IReadOnlyList<EconomicUnit>>(units);
    }

    public Task<EconomicUnit?> GetEconomicUnitByIdAsync(string economicUnitId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, group_id, owner_participant_id, name FROM economic_units WHERE id = $id";
        command.Parameters.AddWithValue("$id", economicUnitId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<EconomicUnit?>(null);
        }

        return Task.FromResult<EconomicUnit?>(new EconomicUnit(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)));
    }

    public Task SaveEconomicUnitAsync(EconomicUnit economicUnit, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "economic_units", economicUnit.Id, economicUnit.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO economic_units (group_id, id, owner_participant_id, name)
VALUES ($groupId, $id, $ownerParticipantId, $name)
ON CONFLICT(id) DO UPDATE SET
  owner_participant_id = excluded.owner_participant_id,
  name = excluded.name";
            command.Parameters.AddWithValue("$groupId", economicUnit.GroupId);
            command.Parameters.AddWithValue("$id", economicUnit.Id);
            command.Parameters.AddWithValue("$ownerParticipantId", economicUnit.OwnerParticipantId);
            command.Parameters.AddWithValue("$name", (object?)economicUnit.Name ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    public Task DeleteEconomicUnitAsync(string economicUnitId, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM economic_units WHERE id = $id";
            command.Parameters.AddWithValue("$id", economicUnitId);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
