using LuSplit.Application.Sync.Ports;
using LuSplit.Domain.Sync;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sync;

/// <summary>
/// SQLite-backed implementation of <see cref="ISyncCursorRepository"/>.
/// Uses the <c>sync_cursors</c> table created by migration V2.
/// </summary>
public sealed class SyncCursorRepositorySqlite : ISyncCursorRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public SyncCursorRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<SyncCursor?> GetAsync(string deviceId, string groupId, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT device_id, group_id, last_synced_hlc_timestamp, last_synced_at
FROM sync_cursors
WHERE device_id = $deviceId AND group_id = $groupId";
        cmd.Parameters.AddWithValue("$deviceId", deviceId);
        cmd.Parameters.AddWithValue("$groupId", groupId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Task.FromResult<SyncCursor?>(null);

        return Task.FromResult<SyncCursor?>(new SyncCursor(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3))));
    }

    public Task SaveAsync(SyncCursor cursor, CancellationToken ct)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO sync_cursors (device_id, group_id, last_synced_hlc_timestamp, last_synced_at)
VALUES ($deviceId, $groupId, $hlc, $syncedAt)
ON CONFLICT(device_id, group_id) DO UPDATE SET
    last_synced_hlc_timestamp = excluded.last_synced_hlc_timestamp,
    last_synced_at = excluded.last_synced_at";
            cmd.Parameters.AddWithValue("$deviceId", cursor.DeviceId);
            cmd.Parameters.AddWithValue("$groupId", cursor.GroupId);
            cmd.Parameters.AddWithValue("$hlc", cursor.LastSyncedHlcTimestamp);
            cmd.Parameters.AddWithValue("$syncedAt", cursor.LastSyncedAt.ToString("o"));
            cmd.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
