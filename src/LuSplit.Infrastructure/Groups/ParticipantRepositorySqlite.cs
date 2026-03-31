using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Groups;

public sealed class ParticipantRepositorySqlite : IParticipantRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public ParticipantRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
FROM participants
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var participants = new List<Participant>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            participants.Add(new Participant(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                SqliteEnumConverters.ParseConsumptionCategory(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return Task.FromResult<IReadOnlyList<Participant>>(participants);
    }

    public Task SaveParticipantAsync(Participant participant, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "participants", participant.Id, participant.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO participants (
  group_id, id, economic_unit_id, name, consumption_category, custom_consumption_weight
) VALUES ($groupId, $id, $economicUnitId, $name, $category, $weight)
ON CONFLICT(id) DO UPDATE SET
  economic_unit_id = excluded.economic_unit_id,
  name = excluded.name,
  consumption_category = excluded.consumption_category,
  custom_consumption_weight = excluded.custom_consumption_weight";
            command.Parameters.AddWithValue("$groupId", participant.GroupId);
            command.Parameters.AddWithValue("$id", participant.Id);
            command.Parameters.AddWithValue("$economicUnitId", participant.EconomicUnitId);
            command.Parameters.AddWithValue("$name", participant.Name);
            command.Parameters.AddWithValue("$category", SqliteEnumConverters.ToConsumptionCategoryString(participant.ConsumptionCategory));
            command.Parameters.AddWithValue("$weight", (object?)participant.CustomConsumptionWeight ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
