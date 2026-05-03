using System.Net;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
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
    public async Task<HttpResponseData> CreateGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups")] HttpRequestData req,
        CancellationToken ct)
    {
        CreateGroupRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateGroupRequest>(
                await req.Body.ToMemoryStreamAsync(), default, ct);
        }
        catch
        {
            return CreateJsonResponse(HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return CreateJsonResponse(HttpStatusCode.BadRequest, "Request body is required.");

        await _store.EnsureTableExistsAsync(ct);

        var existing = await _store.GetGroupAsync(request.GroupId, ct);
        if (existing is not null)
            return CreateJsonResponse(HttpStatusCode.Conflict, $"Group {request.GroupId} already exists.");

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

        return CreateJsonResponse(HttpStatusCode.Created, response);
    }

    [Function("GetGroupInfo")]
    public async Task<HttpResponseData> GetGroupInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups/{groupId}")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var entity = await _store.GetGroupAsync(groupId, ct);
        if (entity is null)
            return CreateJsonResponse(HttpStatusCode.NotFound, $"Group {groupId} not found.");

        var response = new GroupInfoResponse(
            GroupId: groupId,
            OwnerId: entity.GetString("OwnerId"),
            CurrentKeyVersion: entity.GetInt32("CurrentKeyVersion") ?? 1,
            CreatedAt: entity.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.MinValue);

        return CreateJsonResponse(HttpStatusCode.OK, response);
    }

    private static HttpResponseData CreateJsonResponse(HttpStatusCode status, object? value = null)
    {
        var response = Microsoft.Azure.Functions.Worker.Http.HttpResponseData.CreateResponse((int)status);
        if (value is not null)
        {
            response.Headers["Content-Type"] = "application/json";
            response.WriteString(JsonSerializer.Serialize(value));
        }
        return response;
    }
}

