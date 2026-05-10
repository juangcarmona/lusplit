using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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

    [Function("RequestSyncToken")]
    public async Task<HttpResponseData> RequestSyncToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/sync-token")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        SyncTokenRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SyncTokenRequest>(req.Body, (JsonSerializerOptions?)null, ct);
        }
        catch
        {
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, $"Group {groupId} not found.");

        var containerName = GroupMetadataStore.GroupContainerName(groupId);
        var expiresAt = DateTimeOffset.UtcNow.Add(SasExpiry);

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

        return await CreateJsonResponse(req, HttpStatusCode.OK, response);
    }

    private static async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode status, object value)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(value));
        return response;
    }

    private static async Task<HttpResponseData> CreateTextResponse(HttpRequestData req, HttpStatusCode status, string text)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "text/plain");
        await response.WriteStringAsync(text);
        return response;
    }
}
