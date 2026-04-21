using System.Net;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
    public async Task<IActionResult> RevokeMember(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/members/{userId}/revoke")] HttpRequest req,
        string groupId,
        string userId,
        CancellationToken ct)
    {
        RevokeMemberRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<RevokeMemberRequest>(ct);
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

        var ownerId = group.GetString("OwnerId");
        if (!string.Equals(ownerId, request.RevokedByUserId, StringComparison.OrdinalIgnoreCase))
            return new ObjectResult("Only the group owner can revoke members.") { StatusCode = (int)HttpStatusCode.Forbidden };

        if (string.Equals(userId, request.RevokedByUserId, StringComparison.OrdinalIgnoreCase))
            return new BadRequestObjectResult("The owner cannot revoke themselves.");

        // Mark key rotation required (will be handled by KeyRotationFunctions in T103+)
        await _groupStore.SetKeyRotationRequiredAsync(groupId, ct);

        _logger.LogInformation("Member {UserId} revoked from group {GroupId} by {OwnerId}", userId, groupId, request.RevokedByUserId);
        return new NoContentResult();
    }

    [Function("TransferOwnership")]
    public async Task<IActionResult> TransferOwnership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/transfer-ownership")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        TransferOwnershipRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<TransferOwnershipRequest>(ct);
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

        var ownerId = group.GetString("OwnerId");
        if (!string.Equals(ownerId, request.CallerUserId, StringComparison.OrdinalIgnoreCase))
            return new ObjectResult("Only the current owner can transfer ownership.") { StatusCode = (int)HttpStatusCode.Forbidden };

        if (string.Equals(request.NewOwnerUserId, request.CallerUserId, StringComparison.OrdinalIgnoreCase))
            return new BadRequestObjectResult("New owner must be a different user.");

        await _groupStore.UpdateOwnerAsync(groupId, request.NewOwnerUserId, ct);

        _logger.LogInformation("Ownership of group {GroupId} transferred to {NewOwner}", groupId, request.NewOwnerUserId);
        return new NoContentResult();
    }
}
