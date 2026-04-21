using LuSplit.Domain.Activity;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Activity;

/// <summary>
/// Local-only activity log. The <c>activity_entries</c> table is created by SQLite migration V3.
/// </summary>
public sealed class ActivityEntryRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public ActivityEntryRepository(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task InsertAsync(ActivityEntry entry, CancellationToken ct)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO activity_entries (entry_id, group_id, entry_type, actor_user_id, entity_id, description, occurred_at)
VALUES ($entryId, $groupId, $type, $actor, $entityId, $description, $occurredAt)
ON CONFLICT(entry_id) DO NOTHING";
            cmd.Parameters.AddWithValue("$entryId", entry.EntryId);
            cmd.Parameters.AddWithValue("$groupId", entry.GroupId);
            cmd.Parameters.AddWithValue("$type", entry.EntryType.ToString());
            cmd.Parameters.AddWithValue("$actor", entry.ActorUserId);
            cmd.Parameters.AddWithValue("$entityId", (object?)entry.EntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$description", (object?)entry.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$occurredAt", entry.OccurredAt.ToString("o"));
            cmd.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    public Task<IReadOnlyList<ActivityEntry>> ListByGroupAsync(string groupId, int limit, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT entry_id, group_id, entry_type, actor_user_id, entity_id, description, occurred_at
FROM activity_entries
WHERE group_id = $groupId
ORDER BY occurred_at DESC
LIMIT $limit";
        cmd.Parameters.AddWithValue("$groupId", groupId);
        cmd.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ActivityEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new ActivityEntry(
                reader.GetString(0),
                reader.GetString(1),
                Enum.Parse<ActivityEntryType>(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6))));
        }

        return Task.FromResult<IReadOnlyList<ActivityEntry>>(entries);
    }
}
