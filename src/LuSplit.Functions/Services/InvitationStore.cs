using Azure.Data.Tables;

namespace LuSplit.Functions.Services;

public sealed class InvitationStore : IInvitationStore
{
    private const string TableName = "invitations";
    private readonly TableClient _tableClient;

    public InvitationStore(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await _tableClient.CreateIfNotExistsAsync(ct);
    }

    public async Task SaveInvitationAsync(
        string invitationId,
        string groupId,
        string invitedByUserId,
        string invitedByDeviceId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        var entity = new TableEntity(groupId, invitationId)
        {
            ["InvitedByUserId"] = invitedByUserId,
            ["InvitedByDeviceId"] = invitedByDeviceId,
            ["TokenHash"] = tokenHash,
            ["Status"] = "Pending",
            ["ExpiresAt"] = expiresAt,
            ["CreatedAt"] = DateTimeOffset.UtcNow,
        };

        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task<TableEntity?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        // Cross-partition query — acceptable for low-volume invite flow.
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter: $"TokenHash eq '{tokenHash}'",
            cancellationToken: ct))
        {
            return entity;
        }

        return null;
    }

    public async Task<TableEntity?> GetInvitationAsync(string groupId, string invitationId, CancellationToken ct)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(groupId, invitationId, cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpdateStatusAsync(string groupId, string invitationId, string status, CancellationToken ct)
    {
        var entity = new TableEntity(groupId, invitationId)
        {
            ["Status"] = status
        };

        await _tableClient.UpdateEntityAsync(entity, Azure.ETag.All, TableUpdateMode.Merge, ct);
    }
}
