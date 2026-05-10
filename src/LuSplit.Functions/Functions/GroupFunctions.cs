using System.Net;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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

        string body;
        using (var reader = new StreamReader(req.Body))
            body = await reader.ReadToEndAsync(ct);

        try
        {
            request = JsonSerializer.Deserialize<CreateGroupRequest>(body, FunctionJsonOptions.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateGroup rejected: deserialization failed. Body: {Body}", body);
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
        {
            _logger.LogWarning("CreateGroup rejected: null request body");
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.GroupId))
        {
            _logger.LogWarning("CreateGroup rejected: missing GroupId");
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "GroupId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerId))
        {
            _logger.LogWarning("CreateGroup rejected: missing OwnerId for group {GroupId}", request.GroupId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "OwnerId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerDeviceId))
        {
            _logger.LogWarning("CreateGroup rejected: missing OwnerDeviceId for group {GroupId}", request.GroupId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "OwnerDeviceId is required.");
        }

        await _store.EnsureTableExistsAsync(ct);

        var existing = await _store.GetGroupAsync(request.GroupId, ct);
        if (existing is not null)
            return await CreateJsonResponseAsync(req, HttpStatusCode.Conflict, $"Group {request.GroupId} already exists.");

        await _store.SaveGroupAsync(
            request.GroupId,
            request.OwnerId,
            request.OwnerDeviceId,
            request.InitialKeyVersion,
            request.WrappedKeys,
            ct);

        var containerName = GroupMetadataStore.GroupContainerName(request.GroupId);
        var response = new CreateGroupResponse(request.GroupId, containerName);

        _logger.LogInformation(
            "Group {GroupId} registered for owner {OwnerId}",
            request.GroupId,
            request.OwnerId);

        return await CreateJsonResponseAsync(req, HttpStatusCode.Created, response);
    }

    [Function("GetGroupInfo")]
    public async Task<HttpResponseData> GetGroupInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups/{groupId}")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, "GroupId is required.");

        var entity = await _store.GetGroupAsync(groupId, ct);
        if (entity is null)
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, $"Group {groupId} not found.");

        var response = new GroupInfoResponse(
            GroupId: groupId,
            OwnerId: entity.GetString("OwnerId"),
            CurrentKeyVersion: entity.GetInt32("CurrentKeyVersion") ?? 1,
            CreatedAt: entity.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.MinValue);

        return await CreateJsonResponseAsync(req, HttpStatusCode.OK, response);
    }

    private static async Task<HttpResponseData> CreateJsonResponseAsync(
        HttpRequestData req,
        HttpStatusCode status,
        object? value = null)
    {
        var response = req.CreateResponse(status);

        if (value is not null)
        {
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(value, FunctionJsonOptions.Value));
        }

        return response;
    }
}