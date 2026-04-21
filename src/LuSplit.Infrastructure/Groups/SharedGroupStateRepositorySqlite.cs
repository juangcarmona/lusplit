using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Groups;

/// <summary>
/// Reads and writes <see cref="SharedGroupState"/> from the extended columns on the <c>groups</c> table
/// added by migration V2.
/// </summary>
public sealed class SharedGroupStateRepositorySqlite : ISharedGroupStateRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public SharedGroupStateRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<SharedGroupState?> GetByGroupIdAsync(string groupId, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT is_shared, remote_container_name, owner_id, current_key_version, sync_status, is_read_only
FROM groups
WHERE id = $groupId";
        cmd.Parameters.AddWithValue("$groupId", groupId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Task.FromResult<SharedGroupState?>(null);

        var isShared = reader.GetInt32(0) != 0;
        if (!isShared)
            return Task.FromResult<SharedGroupState?>(null);

        return Task.FromResult<SharedGroupState?>(new SharedGroupState(
            IsShared: true,
            RemoteContainerName: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            OwnerId: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            CurrentKeyVersion: reader.GetInt32(3),
            SyncStatus: Enum.TryParse<SyncStatus>(reader.IsDBNull(4) ? null : reader.GetString(4), out var status)
                ? status
                : SyncStatus.UpToDate,
            IsReadOnly: reader.GetInt32(5) != 0));
    }

    public Task SaveAsync(string groupId, SharedGroupState state, CancellationToken ct)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
UPDATE groups
SET is_shared = $isShared,
    remote_container_name = $containerName,
    owner_id = $ownerId,
    current_key_version = $keyVersion,
    sync_status = $syncStatus,
    is_read_only = $isReadOnly
WHERE id = $groupId";
            cmd.Parameters.AddWithValue("$isShared", state.IsShared ? 1 : 0);
            cmd.Parameters.AddWithValue("$containerName", (object?)state.RemoteContainerName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ownerId", (object?)state.OwnerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$keyVersion", state.CurrentKeyVersion);
            cmd.Parameters.AddWithValue("$syncStatus", state.SyncStatus.ToString());
            cmd.Parameters.AddWithValue("$isReadOnly", state.IsReadOnly ? 1 : 0);
            cmd.Parameters.AddWithValue("$groupId", groupId);
            cmd.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
