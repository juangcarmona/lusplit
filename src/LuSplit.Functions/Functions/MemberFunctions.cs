using System.Net;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class MemberFunctions
{
    private readonly IGroupMetadataStore _groupStore;
    private readonly ILogger<MemberFunctions> _logger;

    public MemberFunctions(IGroupMetadataStore groupStore, ILogger<MemberFunctions> logger)
    {
        _groupStore = groupStore;
        _logger = logger;
    }

    [Function("RevokeMember")]
    public async Task<HttpResponseData> RevokeMember(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/members/{userId}/revoke")] HttpRequestData req,
        string groupId,
        string userId,
        CancellationToken ct)
    {
        RevokeMemberRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<RevokeMemberRequest>(req.Body, (JsonSerializerOptions?)null, ct);
        }
        catch
        {
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return CreateTextResponse(req, HttpStatusCode.NotFound, $"Group {groupId} not found.");

        var ownerId = group.GetString("OwnerId");
        if (!string.Equals(ownerId, request.RevokedByUserId, StringComparison.OrdinalIgnoreCase))
            return CreateTextResponse(req, HttpStatusCode.Forbidden, "Only the group owner can revoke members.");

        if (string.Equals(userId, request.RevokedByUserId, StringComparison.OrdinalIgnoreCase))
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "The owner cannot revoke themselves.");

        await _groupStore.SetKeyRotationRequiredAsync(groupId, ct);

        _logger.LogInformation("Member {UserId} revoked from group {GroupId} by {OwnerId}", userId, groupId, request.RevokedByUserId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("TransferOwnership")]
    public async Task<HttpResponseData> TransferOwnership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/transfer-ownership")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        TransferOwnershipRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<TransferOwnershipRequest>(req.Body, (JsonSerializerOptions?)null, ct);
        }
        catch
        {
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return CreateTextResponse(req, HttpStatusCode.NotFound, $"Group {groupId} not found.");

        var ownerId = group.GetString("OwnerId");
        if (!string.Equals(ownerId, request.CallerUserId, StringComparison.OrdinalIgnoreCase))
            return CreateTextResponse(req, HttpStatusCode.Forbidden, "Only the current owner can transfer ownership.");

        if (string.Equals(request.NewOwnerUserId, request.CallerUserId, StringComparison.OrdinalIgnoreCase))
            return CreateTextResponse(req, HttpStatusCode.BadRequest, "New owner must be a different user.");

        await _groupStore.UpdateOwnerAsync(groupId, request.NewOwnerUserId, ct);

        _logger.LogInformation("Ownership of group {GroupId} transferred to {NewOwner}", groupId, request.NewOwnerUserId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static HttpResponseData CreateTextResponse(HttpRequestData req, HttpStatusCode status, string text)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "text/plain");
        _ = response.WriteStringAsync(text);
        return response;
    }
}
