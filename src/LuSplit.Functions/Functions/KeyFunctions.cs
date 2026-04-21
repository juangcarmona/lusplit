using System.Net;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Functions;

public sealed class KeyFunctions
{
    private readonly IKeyStore _keyStore;
    private readonly ILogger<KeyFunctions> _logger;

    public KeyFunctions(IKeyStore keyStore, ILogger<KeyFunctions> logger)
    {
        _keyStore = keyStore;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/groups/{groupId}/keys
    /// Upload a rotated key set. Validates that the new version is strictly monotonic.
    /// </summary>
    [Function("UploadRotatedKey")]
    public async Task<IActionResult> UploadRotatedKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/keys")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        UploadRotatedKeyRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<UploadRotatedKeyRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        if (request is null)
            return new BadRequestObjectResult("Request body is required.");

        if (request.WrappedKeys is null || request.WrappedKeys.Count == 0)
            return new BadRequestObjectResult("At least one wrapped key is required.");

        await _keyStore.EnsureTableExistsAsync(ct);

        // Validate version monotonicity
        var currentVersion = await _keyStore.GetCurrentKeyVersionAsync(groupId, ct);
        if (request.NewKeyVersion <= currentVersion)
        {
            return new ObjectResult($"New key version {request.NewKeyVersion} must be greater than current version {currentVersion}.")
            {
                StatusCode = (int)HttpStatusCode.Conflict
            };
        }

        // Store wrapped keys for each device
        foreach (var entry in request.WrappedKeys)
        {
            await _keyStore.SaveWrappedKeyAsync(groupId, request.NewKeyVersion, entry.DeviceId, entry.WrappedKey, ct);
        }

        _logger.LogInformation("Uploaded key version {Version} for group {GroupId} with {Count} device keys",
            request.NewKeyVersion, groupId, request.WrappedKeys.Count);

        return new OkResult();
    }

    /// <summary>
    /// GET /api/groups/{groupId}/keys?deviceId={deviceId}
    /// Returns the full wrapped key chain for a device.
    /// </summary>
    [Function("GetWrappedKeysForDevice")]
    public async Task<IActionResult> GetWrappedKeysForDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups/{groupId}/keys")] HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        var deviceId = req.Query["deviceId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceId))
            return new BadRequestObjectResult("deviceId query parameter is required.");

        await _keyStore.EnsureTableExistsAsync(ct);

        var keyVersions = await _keyStore.GetAllWrappedKeysForDeviceAsync(groupId, deviceId, ct);

        var response = new GetWrappedKeysForDeviceResponse(
            keyVersions.Select(k => new GroupKeyVersionDto(k.KeyVersion, k.WrappedKey)).ToList());

        return new OkObjectResult(response);
    }
}
