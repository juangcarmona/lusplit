using System.Net.Http.Json;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.Sync;

/// <summary>
/// Requests a short-lived SAS token from the control plane and caches it
/// until near-expiry to avoid excessive round-trips.
/// </summary>
public sealed class SasTokenProvider
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    private readonly ControlPlane.ControlPlaneHttpClient _httpClient;

    // Keyed by groupId
    private readonly Dictionary<string, (SyncTokenResponse Token, DateTimeOffset FetchedAt)> _cache = new();

    public SasTokenProvider(ControlPlane.ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SyncTokenResponse> GetTokenAsync(string groupId, string deviceId, CancellationToken ct)
    {
        if (_cache.TryGetValue(groupId, out var cached) &&
            cached.Token.ExpiresAt - ExpiryBuffer > DateTimeOffset.UtcNow)
            return cached.Token;

        var request = new SyncTokenRequest(groupId, deviceId);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"api/groups/{Uri.EscapeDataString(groupId)}/sync-token")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<SyncTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty sync token response.");

        _cache[groupId] = (token, DateTimeOffset.UtcNow);
        return token;
    }

    public async Task<Uri> GetContainerSasUriAsync(string containerName, CancellationToken ct)
    {
        // Get token for the group that owns this container.
        // Container name encodes the group ID: "grp-{groupId}"
        var groupId = containerName.StartsWith("grp-") ? containerName[4..] : containerName;

        // We don't have deviceId in context here — caller must use GetTokenAsync
        // which caches. If cache miss, use a placeholder device context.
        if (!_cache.TryGetValue(groupId, out var cached) ||
            cached.Token.ExpiresAt - ExpiryBuffer <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException($"No valid SAS token for container '{containerName}'. Call GetTokenAsync first.");

        // Build the container SAS URI from the SAS token
        return new Uri($"https://storage.blob.core.windows.net/{containerName}?{cached.Token.SasToken}");
    }
}
