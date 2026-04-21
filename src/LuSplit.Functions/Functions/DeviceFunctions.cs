using System.Net;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
    public async Task<IActionResult> RegisterDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/register")] HttpRequest req,
        CancellationToken ct)
    {
        RegisterDeviceRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<RegisterDeviceRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        if (request is null)
            return new BadRequestObjectResult("Request body is required.");

        // Extract userId from claims (placeholder — proper auth in T103+)
        var userId = req.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return new ObjectResult("X-User-Id header is required.") { StatusCode = (int)HttpStatusCode.Unauthorized };

        await _deviceStore.EnsureTableExistsAsync(ct);
        await _deviceStore.SaveDeviceAsync(
            request.DeviceId, userId, request.DeviceName, request.Platform, request.PublicKey, ct);

        _logger.LogInformation("Device {DeviceId} registered for user {UserId}", request.DeviceId, userId);

        return new ObjectResult(new RegisterDeviceResponse(request.DeviceId)) { StatusCode = (int)HttpStatusCode.Created };
    }

    [Function("ListDevices")]
    public async Task<IActionResult> ListDevices(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "devices")] HttpRequest req,
        CancellationToken ct)
    {
        var userId = req.Query["userId"].FirstOrDefault()
            ?? req.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId))
            return new BadRequestObjectResult("userId query parameter or X-User-Id header is required.");

        await _deviceStore.EnsureTableExistsAsync(ct);
        var entities = await _deviceStore.ListDevicesAsync(userId, ct);

        var devices = entities.Select(e => new DeviceDto(
            e.RowKey,
            e.GetString("DeviceName") ?? string.Empty,
            e.GetString("Platform") ?? string.Empty,
            e.GetDateTimeOffset("RegisteredAt") ?? DateTimeOffset.MinValue,
            e.GetBoolean("IsRevoked") ?? false)).ToList();

        return new OkObjectResult(new ListDevicesResponse(devices));
    }

    [Function("RevokeDevice")]
    public async Task<IActionResult> RevokeDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/{deviceId}/revoke")] HttpRequest req,
        string deviceId,
        CancellationToken ct)
    {
        var userId = req.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return new ObjectResult("X-User-Id header is required.") { StatusCode = (int)HttpStatusCode.Unauthorized };

        await _deviceStore.EnsureTableExistsAsync(ct);
        var entity = await _deviceStore.GetDeviceAsync(userId, deviceId, ct);
        if (entity is null)
            return new NotFoundObjectResult($"Device {deviceId} not found.");

        await _deviceStore.RevokeDeviceAsync(userId, deviceId, ct);

        _logger.LogInformation("Device {DeviceId} revoked for user {UserId}", deviceId, userId);
        return new NoContentResult();
    }
}
