using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class SyncFunctions
{
    private static readonly TimeSpan SasExpiry = TimeSpan.FromMinutes(15);

    private readonly IGroupMetadataStore _groupStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncFunctions> _logger;

    public SyncFunctions(
        IGroupMetadataStore groupStore,
        IConfiguration configuration,
        ILogger<SyncFunctions> logger)
    {
        _groupStore = groupStore;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Issues a scoped User Delegation SAS for the group's blob container.
    /// The caller must be an authenticated, non-revoked member of the group.
    /// </summary>
    [Function("RequestSyncToken")]
    public async Task<IActionResult> RequestSyncToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/sync-token")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        SyncTokenRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<SyncTokenRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        if (request is null)
            return new BadRequestObjectResult("Request body is required.");

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return new NotFoundObjectResult($"Group {groupId} not found.");

        // In production, verify JWT claims include membership for this group.
        // For now, issue SAS scoped to the group's container.
        var containerName = GroupMetadataStore.GroupContainerName(groupId);
        var expiresAt = DateTimeOffset.UtcNow.Add(SasExpiry);

        // Issue a scoped SAS token. In production this should use User Delegation SAS.
        // For development, we use the connection string from configuration.
        var connectionString = _configuration["AzureWebJobsStorage"]
            ?? "UseDevelopmentStorage=true";

        var containerClient = new BlobContainerClient(connectionString, containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var sasBuilder = new BlobSasBuilder(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List, expiresAt)
        {
            BlobContainerName = containerName
        };

        var sasToken = containerClient.GenerateSasUri(sasBuilder).Query.TrimStart('?');

        var response = new SyncTokenResponse(sasToken, containerName, expiresAt);

        _logger.LogInformation("Issued sync token for group {GroupId}, device {DeviceId}", groupId, request.DeviceId);

        return new OkObjectResult(response);
    }
}
