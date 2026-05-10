using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class InvitationFunctions
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);

    private readonly IInvitationStore _store;
    private readonly IGroupMetadataStore _groupStore;
    private readonly ILogger<InvitationFunctions> _logger;

    public InvitationFunctions(IInvitationStore store, IGroupMetadataStore groupStore, ILogger<InvitationFunctions> logger)
    {
        _store = store;
        _groupStore = groupStore;
        _logger = logger;
    }

    [Function("CreateInvitation")]
    public async Task<HttpResponseData> CreateInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/invitations")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        CreateInvitationRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateInvitationRequest>(req.Body, (JsonSerializerOptions?)null, ct);
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

        var ownerId = group.GetString("OwnerId");
        if (ownerId != request.InvitedByUserId)
            return await CreateTextResponse(req, (HttpStatusCode)403, "Only the group owner may create invitations.");

        var invitationId = Guid.NewGuid().ToString("N");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var expiresAt = DateTimeOffset.UtcNow.Add(DefaultExpiry);

        await _store.EnsureTableExistsAsync(ct);
        await _store.SaveInvitationAsync(
            invitationId, groupId,
            request.InvitedByUserId, request.InvitedByDeviceId,
            tokenHash, expiresAt, ct);

        _logger.LogInformation("Invitation {InvitationId} created for group {GroupId}", invitationId, groupId);

        return await CreateJsonResponse(req, HttpStatusCode.Created, new CreateInvitationResponse(invitationId, token, expiresAt));
    }

    [Function("CancelInvitation")]
    public async Task<HttpResponseData> CancelInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "groups/{groupId}/invitations/{invitationId}")] HttpRequestData req,
        string groupId,
        string invitationId,
        CancellationToken ct)
    {
        var entity = await _store.GetInvitationAsync(groupId, invitationId, ct);
        if (entity is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, $"Invitation {invitationId} not found.");

        await _store.UpdateStatusAsync(groupId, invitationId, "Cancelled", ct);

        _logger.LogInformation("Invitation {InvitationId} cancelled", invitationId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("GetInvitationInfo")]
    public async Task<HttpResponseData> GetInvitationInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invitations/{token}/info")] HttpRequestData req,
        string token,
        CancellationToken ct)
    {
        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, "Invitation not found.");

        var status = entity.GetString("Status") ?? "Unknown";
        var expiresAt = entity.GetDateTimeOffset("ExpiresAt") ?? DateTimeOffset.MinValue;
        var groupId = entity.PartitionKey;

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        var groupName = group?.GetString("GroupName") ?? groupId;
        var invitedByUserId = entity.GetString("InvitedByUserId") ?? string.Empty;

        var response = new InvitationInfoResponse(
            entity.RowKey,
            groupId,
            groupName,
            invitedByUserId,
            expiresAt,
            status);

        return await CreateJsonResponse(req, HttpStatusCode.OK, response);
    }

    [Function("AcceptInvitation")]
    public async Task<HttpResponseData> AcceptInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invitations/{token}/accept")] HttpRequestData req,
        string token,
        CancellationToken ct)
    {
        AcceptInvitationRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<AcceptInvitationRequest>(req.Body, (JsonSerializerOptions?)null, ct);
        }
        catch
        {
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, "Invitation not found.");

        var status = entity.GetString("Status");
        if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            return await CreateTextResponse(req, HttpStatusCode.Conflict, $"Invitation is no longer pending. Status: {status}");

        var expiresAt = entity.GetDateTimeOffset("ExpiresAt") ?? DateTimeOffset.MinValue;
        if (expiresAt < DateTimeOffset.UtcNow)
            return await CreateTextResponse(req, (HttpStatusCode)410, "Invitation has expired.");

        var groupId = entity.PartitionKey;
        var invitationId = entity.RowKey;

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, $"Group {groupId} not found.");

        await _store.UpdateStatusAsync(groupId, invitationId, "Accepted", ct);

        _logger.LogInformation("Invitation {InvitationId} accepted by user {UserId} on device {DeviceId}",
            invitationId, request.AcceptingUserId, request.AcceptingDeviceId);

        var containerName = group.GetString("ContainerName") ?? string.Empty;

        var response = new AcceptInvitationResponse(
            groupId,
            containerName,
            Array.Empty<WrappedKeyEntryDto>());

        return await CreateJsonResponse(req, HttpStatusCode.OK, response);
    }

    [Function("DeclineInvitation")]
    public async Task<HttpResponseData> DeclineInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invitations/{token}/decline")] HttpRequestData req,
        string token,
        CancellationToken ct)
    {
        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, "Invitation not found.");

        await _store.UpdateStatusAsync(entity.PartitionKey, entity.RowKey, "Declined", ct);

        _logger.LogInformation("Invitation {InvitationId} declined", entity.RowKey);
        return req.CreateResponse(HttpStatusCode.NoContent);
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

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }
}
