using System.Net;
using System.Security.Cryptography;
using System.Text;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
    public async Task<IActionResult> CreateInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/invitations")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        CreateInvitationRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<CreateInvitationRequest>(ct);
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

        // Verify owner
        var ownerId = group.GetString("OwnerId");
        if (ownerId != request.InvitedByUserId)
            return new ObjectResult("Only the group owner may create invitations.") { StatusCode = 403 };

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

        var response = new CreateInvitationResponse(invitationId, token, expiresAt);
        return new ObjectResult(response) { StatusCode = (int)HttpStatusCode.Created };
    }

    [Function("CancelInvitation")]
    public async Task<IActionResult> CancelInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "groups/{groupId}/invitations/{invitationId}")] HttpRequest req,
        string groupId,
        string invitationId,
        CancellationToken ct)
    {
        var entity = await _store.GetInvitationAsync(groupId, invitationId, ct);
        if (entity is null)
            return new NotFoundObjectResult($"Invitation {invitationId} not found.");

        await _store.UpdateStatusAsync(groupId, invitationId, "Cancelled", ct);

        _logger.LogInformation("Invitation {InvitationId} cancelled", invitationId);
        return new NoContentResult();
    }

    [Function("GetInvitationInfo")]
    public async Task<IActionResult> GetInvitationInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invitations/{token}/info")] HttpRequest req,
        string token,
        CancellationToken ct)
    {
        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return new NotFoundObjectResult("Invitation not found.");

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

        return new OkObjectResult(response);
    }

    [Function("AcceptInvitation")]
    public async Task<IActionResult> AcceptInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invitations/{token}/accept")] HttpRequest req,
        string token,
        CancellationToken ct)
    {
        AcceptInvitationRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<AcceptInvitationRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        if (request is null)
            return new BadRequestObjectResult("Request body is required.");

        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return new NotFoundObjectResult("Invitation not found.");

        var status = entity.GetString("Status");
        if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            return new ConflictObjectResult($"Invitation is no longer pending. Status: {status}");

        var expiresAt = entity.GetDateTimeOffset("ExpiresAt") ?? DateTimeOffset.MinValue;
        if (expiresAt < DateTimeOffset.UtcNow)
            return new ObjectResult("Invitation has expired.") { StatusCode = 410 };

        var groupId = entity.PartitionKey;
        var invitationId = entity.RowKey;

        var group = await _groupStore.GetGroupAsync(groupId, ct);
        if (group is null)
            return new NotFoundObjectResult($"Group {groupId} not found.");

        // Atomically mark invitation as consumed
        await _store.UpdateStatusAsync(groupId, invitationId, "Accepted", ct);

        _logger.LogInformation("Invitation {InvitationId} accepted by user {UserId} on device {DeviceId}",
            invitationId, request.AcceptingUserId, request.AcceptingDeviceId);

        var containerName = group.GetString("ContainerName") ?? string.Empty;

        // Wrapped keys are distributed by the key rotation flow (T103).
        // For Phase 6, return the container name so the client can begin syncing.
        var response = new AcceptInvitationResponse(
            groupId,
            containerName,
            Array.Empty<WrappedKeyEntryDto>());

        return new OkObjectResult(response);
    }

    [Function("DeclineInvitation")]
    public async Task<IActionResult> DeclineInvitation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invitations/{token}/decline")] HttpRequest req,
        string token,
        CancellationToken ct)
    {
        var tokenHash = HashToken(token);
        await _store.EnsureTableExistsAsync(ct);
        var entity = await _store.GetInvitationByTokenHashAsync(tokenHash, ct);
        if (entity is null)
            return new NotFoundObjectResult("Invitation not found.");

        await _store.UpdateStatusAsync(entity.PartitionKey, entity.RowKey, "Declined", ct);

        _logger.LogInformation("Invitation {InvitationId} declined", entity.RowKey);
        return new NoContentResult();
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }
}
