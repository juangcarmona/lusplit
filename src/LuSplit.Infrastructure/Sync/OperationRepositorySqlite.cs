using LuSplit.Application.Sync.Ports;
using LuSplit.Domain.Sync;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sync;

/// <summary>
/// SQLite-backed implementation of <see cref="IOperationRepository"/>.
/// Uses the <c>operations</c> table created by migration V2.
/// </summary>
public sealed class OperationRepositorySqlite : IOperationRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public OperationRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task SaveAsync(Operation operation, CancellationToken ct)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO operations (operation_id, group_id, device_id, user_id, hlc_timestamp, operation_type, entity_id, encrypted_payload, key_version, created_at)
VALUES ($opId, $groupId, $deviceId, $userId, $hlc, $opType, $entityId, $payload, $keyVersion, $createdAt)
ON CONFLICT(operation_id) DO NOTHING";
            cmd.Parameters.AddWithValue("$opId", operation.OperationId);
            cmd.Parameters.AddWithValue("$groupId", operation.GroupId);
            cmd.Parameters.AddWithValue("$deviceId", operation.DeviceId);
            cmd.Parameters.AddWithValue("$userId", operation.UserId);
            cmd.Parameters.AddWithValue("$hlc", operation.HlcTimestamp);
            cmd.Parameters.AddWithValue("$opType", operation.OperationType.ToString());
            cmd.Parameters.AddWithValue("$entityId", operation.EntityId);
            cmd.Parameters.AddWithValue("$payload", operation.EncryptedPayload);
            cmd.Parameters.AddWithValue("$keyVersion", operation.KeyVersion);
            cmd.Parameters.AddWithValue("$createdAt", operation.CreatedAt.ToString("o"));
            cmd.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    public Task<IReadOnlyList<Operation>> GetPendingAsync(string groupId, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT operation_id, group_id, device_id, user_id, hlc_timestamp, operation_type, entity_id, encrypted_payload, key_version, created_at
FROM operations
WHERE group_id = $groupId
ORDER BY hlc_timestamp ASC";
        cmd.Parameters.AddWithValue("$groupId", groupId);

        var ops = new List<Operation>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ops.Add(ReadOperation(reader));
        }

        return Task.FromResult<IReadOnlyList<Operation>>(ops);
    }

    public Task MarkSyncedAsync(IReadOnlyList<string> operationIds, CancellationToken ct)
    {
        // For now, marking synced means we keep the row for idempotency (ExistsAsync) but could set a flag.
        // The current schema has no "synced" column; ExistsAsync checks for presence.
        // This is intentionally a no-op — records persist for idempotency.
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string operationId, CancellationToken ct)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM operations WHERE operation_id = $opId";
        cmd.Parameters.AddWithValue("$opId", operationId);
        var count = (long)(cmd.ExecuteScalar() ?? 0L);
        return Task.FromResult(count > 0);
    }

    private static Operation ReadOperation(SqliteDataReader reader)
        => new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            Enum.Parse<OperationType>(reader.GetString(5)),
            reader.GetString(6),
            (byte[])reader.GetValue(7),
            reader.GetInt32(8),
            DateTimeOffset.Parse(reader.GetString(9)));
}
