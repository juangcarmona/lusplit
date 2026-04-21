using Azure.Data.Tables;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Functions.Services;

public sealed class GroupMetadataStore : IGroupMetadataStore
{
    private const string TableName = "groups";
    private readonly TableClient _tableClient;

    public GroupMetadataStore(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await _tableClient.CreateIfNotExistsAsync(ct);
    }

    public async Task SaveGroupAsync(
        string groupId,
        string ownerId,
        string ownerDeviceId,
        int keyVersion,
        IReadOnlyList<WrappedKeyEntryDto> wrappedKeys,
        CancellationToken ct)
    {
        var entity = new TableEntity("groups", groupId)
        {
            ["OwnerId"] = ownerId,
            ["OwnerDeviceId"] = ownerDeviceId,
            ["CurrentKeyVersion"] = keyVersion,
            ["WrappedKeysJson"] = System.Text.Json.JsonSerializer.Serialize(wrappedKeys),
            ["ContainerName"] = GroupContainerName(groupId),
            ["CreatedAt"] = DateTimeOffset.UtcNow,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task<TableEntity?> GetGroupAsync(string groupId, CancellationToken ct)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>("groups", groupId, cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public static string GroupContainerName(string groupId) =>
        $"grp-{groupId.ToLowerInvariant().Replace("-", "")}";

    public async Task SetKeyRotationRequiredAsync(string groupId, CancellationToken ct)
    {
        var entity = new TableEntity("groups", groupId) { ["KeyRotationRequired"] = true };
        await _tableClient.UpdateEntityAsync(entity, Azure.ETag.All, TableUpdateMode.Merge, ct);
    }

    public async Task UpdateOwnerAsync(string groupId, string newOwnerId, CancellationToken ct)
    {
        var entity = new TableEntity("groups", groupId) { ["OwnerId"] = newOwnerId };
        await _tableClient.UpdateEntityAsync(entity, Azure.ETag.All, TableUpdateMode.Merge, ct);
    }
}
