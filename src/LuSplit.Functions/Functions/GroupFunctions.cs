using System.Net;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class GroupFunctions
{
    private readonly IGroupMetadataStore _store;
    private readonly ILogger<GroupFunctions> _logger;

    public GroupFunctions(IGroupMetadataStore store, ILogger<GroupFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Function("CreateGroup")]
    public async Task<IActionResult> CreateGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups")] HttpRequest req,
        CancellationToken ct)
    {
        CreateGroupRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<CreateGroupRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        if (request is null)
            return new BadRequestObjectResult("Request body is required.");

        var existing = await _store.GetGroupAsync(request.GroupId, ct);
        if (existing is not null)
            return new ConflictObjectResult($"Group {request.GroupId} already exists.");

        await _store.EnsureTableExistsAsync(ct);
        await _store.SaveGroupAsync(
            request.GroupId,
            request.OwnerId,
            request.OwnerDeviceId,
            request.InitialKeyVersion,
            request.WrappedKeys,
            ct);

        var containerName = GroupMetadataStore.GroupContainerName(request.GroupId);
        var response = new CreateGroupResponse(request.GroupId, containerName);

        _logger.LogInformation("Group {GroupId} registered for owner {OwnerId}", request.GroupId, request.OwnerId);

        return new ObjectResult(response) { StatusCode = (int)HttpStatusCode.Created };
    }

    [Function("GetGroupInfo")]
    public async Task<IActionResult> GetGroupInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups/{groupId}")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        var entity = await _store.GetGroupAsync(groupId, ct);
        if (entity is null)
            return new NotFoundObjectResult($"Group {groupId} not found.");

        var response = new GroupInfoResponse(
            GroupId: groupId,
            OwnerId: entity.GetString("OwnerId"),
            CurrentKeyVersion: entity.GetInt32("CurrentKeyVersion") ?? 1,
            CreatedAt: entity.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.MinValue);

        return new OkObjectResult(response);
    }
}
