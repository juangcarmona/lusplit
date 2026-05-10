using System.Net;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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

    [Function("UploadRotatedKey")]
    public async Task<HttpResponseData> UploadRotatedKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/{groupId}/keys")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        UploadRotatedKeyRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UploadRotatedKeyRequest>(req.Body, FunctionJsonOptions.Value, ct);
        }
        catch
        {
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");
        }

        if (request is null)
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is required.");

        if (request.WrappedKeys is null || request.WrappedKeys.Count == 0)
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "At least one wrapped key is required.");

        await _keyStore.EnsureTableExistsAsync(ct);

        var currentVersion = await _keyStore.GetCurrentKeyVersionAsync(groupId, ct);
        if (request.NewKeyVersion <= currentVersion)
        {
            return await CreateTextResponse(req, HttpStatusCode.Conflict,
                $"New key version {request.NewKeyVersion} must be greater than current version {currentVersion}.");
        }

        foreach (var entry in request.WrappedKeys)
        {
            await _keyStore.SaveWrappedKeyAsync(groupId, request.NewKeyVersion, entry.DeviceId, entry.WrappedKey, ct);
        }

        _logger.LogInformation("Uploaded key version {Version} for group {GroupId} with {Count} device keys",
            request.NewKeyVersion, groupId, request.WrappedKeys.Count);

        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("GetWrappedKeysForDevice")]
    public async Task<HttpResponseData> GetWrappedKeysForDevice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups/{groupId}/keys")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var deviceId = GetQueryParameter(req, "deviceId");
        if (string.IsNullOrWhiteSpace(deviceId))
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "deviceId query parameter is required.");

        await _keyStore.EnsureTableExistsAsync(ct);

        var keyVersions = await _keyStore.GetAllWrappedKeysForDeviceAsync(groupId, deviceId, ct);

        var response = new GetWrappedKeysForDeviceResponse(
            keyVersions.Select(k => new GroupKeyVersionDto(k.KeyVersion, k.WrappedKey)).ToList());

        return await CreateJsonResponse(req, HttpStatusCode.OK, response);
    }

    private static async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode status, object value)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(value, FunctionJsonOptions.Value));
        return response;
    }

    private static async Task<HttpResponseData> CreateTextResponse(HttpRequestData req, HttpStatusCode status, string text)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "text/plain");
        await response.WriteStringAsync(text);
        return response;
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
