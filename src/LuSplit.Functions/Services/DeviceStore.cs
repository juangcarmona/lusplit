using Azure.Data.Tables;

namespace LuSplit.Functions.Services;

public interface IDeviceStore
{
    Task EnsureTableExistsAsync(CancellationToken ct);
    Task SaveDeviceAsync(string deviceId, string userId, string deviceName, string platform, byte[] publicKey, CancellationToken ct);
    Task<TableEntity?> GetDeviceAsync(string userId, string deviceId, CancellationToken ct);
    Task<IReadOnlyList<TableEntity>> ListDevicesAsync(string userId, CancellationToken ct);
    Task RevokeDeviceAsync(string userId, string deviceId, CancellationToken ct);
}

public sealed class DeviceStore : IDeviceStore
{
    private const string TableName = "devices";
    private readonly TableClient _tableClient;

    public DeviceStore(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await _tableClient.CreateIfNotExistsAsync(ct);
    }

    public async Task SaveDeviceAsync(
        string deviceId,
        string userId,
        string deviceName,
        string platform,
        byte[] publicKey,
        CancellationToken ct)
    {
        var entity = new TableEntity(userId, deviceId)
        {
            ["DeviceName"] = deviceName,
            ["Platform"] = platform,
            ["PublicKey"] = Convert.ToBase64String(publicKey),
            ["IsRevoked"] = false,
            ["RegisteredAt"] = DateTimeOffset.UtcNow
        };

        // Upsert — idempotent on re-registration
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge, ct);
    }

    public async Task<TableEntity?> GetDeviceAsync(string userId, string deviceId, CancellationToken ct)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(userId, deviceId, cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TableEntity>> ListDevicesAsync(string userId, CancellationToken ct)
    {
        var results = new List<TableEntity>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{userId}'",
            cancellationToken: ct))
        {
            results.Add(entity);
        }
        return results;
    }

    public async Task RevokeDeviceAsync(string userId, string deviceId, CancellationToken ct)
    {
        var entity = await GetDeviceAsync(userId, deviceId, ct);
        if (entity is null) return;

        entity["IsRevoked"] = true;
        await _tableClient.UpdateEntityAsync(entity, entity.ETag, cancellationToken: ct);
    }
}
