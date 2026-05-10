using System.Net;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class DeviceFunctions
{
    private readonly IDeviceStore _deviceStore;
    private readonly ILogger<DeviceFunctions> _logger;

    public DeviceFunctions(IDeviceStore deviceStore, ILogger<DeviceFunctions> logger)
    {
        _deviceStore = deviceStore;
        _logger = logger;
    }

    [Function("RegisterDevice")]
    public async Task<HttpResponseData> RegisterDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/register")] HttpRequestData req,
        CancellationToken ct)
    {
        RegisterDeviceRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<RegisterDeviceRequest>(req.Body, FunctionJsonOptions.Value, ct);
        }
        catch
        {
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        var userId = GetHeader(req, "X-User-Id");
        if (string.IsNullOrWhiteSpace(userId))
            return await CreateTextResponse(req, HttpStatusCode.Unauthorized, "X-User-Id header is required.");

        await _deviceStore.EnsureTableExistsAsync(ct);
        await _deviceStore.SaveDeviceAsync(
            request.DeviceId, userId, request.DeviceName, request.Platform, request.PublicKey, ct);

        _logger.LogInformation("Device {DeviceId} registered for user {UserId}", request.DeviceId, userId);

        return await CreateJsonResponse(req, HttpStatusCode.Created, new RegisterDeviceResponse(request.DeviceId));
    }

    [Function("ListDevices")]
    public async Task<HttpResponseData> ListDevices(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "devices")] HttpRequestData req,
        CancellationToken ct)
    {
        var userId = GetQueryParameter(req, "userId")
            ?? GetHeader(req, "X-User-Id");

        if (string.IsNullOrWhiteSpace(userId))
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "userId query parameter or X-User-Id header is required.");

        await _deviceStore.EnsureTableExistsAsync(ct);
        var entities = await _deviceStore.ListDevicesAsync(userId, ct);

        var devices = entities.Select(e => new DeviceDto(
            e.RowKey,
            e.GetString("DeviceName") ?? string.Empty,
            e.GetString("Platform") ?? string.Empty,
            e.GetDateTimeOffset("RegisteredAt") ?? DateTimeOffset.MinValue,
            e.GetBoolean("IsRevoked") ?? false)).ToList();

        return await CreateJsonResponse(req, HttpStatusCode.OK, new ListDevicesResponse(devices));
    }

    [Function("RevokeDevice")]
    public async Task<HttpResponseData> RevokeDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/{deviceId}/revoke")] HttpRequestData req,
        string deviceId,
        CancellationToken ct)
    {
        var userId = GetHeader(req, "X-User-Id");
        if (string.IsNullOrWhiteSpace(userId))
            return await CreateTextResponse(req, HttpStatusCode.Unauthorized, "X-User-Id header is required.");

        await _deviceStore.EnsureTableExistsAsync(ct);
        var entity = await _deviceStore.GetDeviceAsync(userId, deviceId, ct);
        if (entity is null)
            return await CreateTextResponse(req, HttpStatusCode.NotFound, $"Device {deviceId} not found.");

        await _deviceStore.RevokeDeviceAsync(userId, deviceId, ct);

        _logger.LogInformation("Device {DeviceId} revoked for user {UserId}", deviceId, userId);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode status, object value)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(value, FunctionJsonOptions.Value));
        return response;
    }

    private async Task<HttpResponseData> CreateTextResponse(HttpRequestData req, HttpStatusCode status, string text)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "text/plain");
        await response.WriteStringAsync(text);
        return response;
    }

    private static string? GetHeader(HttpRequestData req, string name)
    {
        foreach (var kvp in req.Headers)
            if (kvp.Key.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return kvp.Value.FirstOrDefault();
        return null;
    }

    private static string? GetQueryParameter(HttpRequestData req, string name)
    {
        if (!req.Url.Query.StartsWith("?")) return null;
        var query = req.Url.Query.Substring(1);
        foreach (var param in query.Split('&'))
        {
            var parts = param.Split('=');
            if (parts.Length == 2 && System.Net.WebUtility.UrlDecode(parts[0]) == name)
                return System.Net.WebUtility.UrlDecode(parts[1]);
        }
        return null;
    }
}
