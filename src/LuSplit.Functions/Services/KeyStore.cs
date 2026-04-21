using Azure.Data.Tables;

namespace LuSplit.Functions.Services;

public interface IKeyStore
{
    Task EnsureTableExistsAsync(CancellationToken ct);
    Task SaveWrappedKeyAsync(string groupId, int keyVersion, string deviceId, byte[] wrappedKey, CancellationToken ct);
    Task<byte[]?> GetWrappedKeyAsync(string groupId, int keyVersion, string deviceId, CancellationToken ct);
    Task<IReadOnlyList<TableEntity>> ListWrappedKeysForVersionAsync(string groupId, int keyVersion, CancellationToken ct);
    Task<int> GetCurrentKeyVersionAsync(string groupId, CancellationToken ct);
    Task<IReadOnlyList<(string DeviceId, int KeyVersion, byte[] WrappedKey)>> GetAllWrappedKeysForDeviceAsync(string groupId, string deviceId, CancellationToken ct);
}

public sealed class KeyStore : IKeyStore
{
    private const string TableName = "groupkeys";
    private readonly TableClient _tableClient;

    public KeyStore(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await _tableClient.CreateIfNotExistsAsync(ct);
    }

    public async Task SaveWrappedKeyAsync(
        string groupId, int keyVersion, string deviceId, byte[] wrappedKey, CancellationToken ct)
    {
        // PartitionKey: {groupId}:{keyVersion}  RowKey: {deviceId}
        var partitionKey = $"{groupId}:{keyVersion}";
        var entity = new TableEntity(partitionKey, deviceId)
        {
            ["GroupId"] = groupId,
            ["KeyVersion"] = keyVersion,
            ["DeviceId"] = deviceId,
            ["WrappedKey"] = Convert.ToBase64String(wrappedKey),
            ["CreatedAt"] = DateTimeOffset.UtcNow
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task<byte[]?> GetWrappedKeyAsync(
        string groupId, int keyVersion, string deviceId, CancellationToken ct)
    {
        try
        {
            var partitionKey = $"{groupId}:{keyVersion}";
            var response = await _tableClient.GetEntityAsync<TableEntity>(partitionKey, deviceId, cancellationToken: ct);
            var base64 = response.Value.GetString("WrappedKey");
            return base64 is null ? null : Convert.FromBase64String(base64);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TableEntity>> ListWrappedKeysForVersionAsync(
        string groupId, int keyVersion, CancellationToken ct)
    {
        var partitionKey = $"{groupId}:{keyVersion}";
        var results = new List<TableEntity>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{partitionKey}'", cancellationToken: ct))
        {
            results.Add(entity);
        }
        return results;
    }

    public async Task<int> GetCurrentKeyVersionAsync(string groupId, CancellationToken ct)
    {
        // Query all partitions starting with groupId: and find the max KeyVersion
        var results = _tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey ge '{groupId}:' and PartitionKey lt '{groupId};'",
            cancellationToken: ct);

        int max = 0;
        await foreach (var entity in results)
        {
            var version = entity.GetInt32("KeyVersion") ?? 0;
            if (version > max) max = version;
        }
        return max;
    }

    public async Task<IReadOnlyList<(string DeviceId, int KeyVersion, byte[] WrappedKey)>> GetAllWrappedKeysForDeviceAsync(
        string groupId, string deviceId, CancellationToken ct)
    {
        var results = new List<(string, int, byte[])>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter: $"RowKey eq '{deviceId}'", cancellationToken: ct))
        {
            var pk = entity.PartitionKey; // format: {groupId}:{keyVersion}
            if (!pk.StartsWith(groupId + ":")) continue;
            if (!int.TryParse(pk[(groupId.Length + 1)..], out var version)) continue;
            var base64 = entity.GetString("WrappedKey");
            if (base64 is null) continue;
            results.Add((deviceId, version, Convert.FromBase64String(base64)));
        }
        return results;
    }
}
