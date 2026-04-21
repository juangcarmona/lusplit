using System.Net.Http.Json;
using LuSplit.Application.KeyManagement.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class KeyRotationAdapter : IKeyRotationPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public KeyRotationAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UploadRotatedKeyAsync(string groupId, UploadRotatedKeyRequest request, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"api/groups/{Uri.EscapeDataString(groupId)}/keys")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<GetWrappedKeysForDeviceResponse> GetWrappedKeysForDeviceAsync(string groupId, string deviceId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"api/groups/{Uri.EscapeDataString(groupId)}/keys?deviceId={Uri.EscapeDataString(deviceId)}");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GetWrappedKeysForDeviceResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task<IReadOnlyList<DevicePublicKeyDto>> GetDevicePublicKeysAsync(string groupId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"api/groups/{Uri.EscapeDataString(groupId)}/keys/devices");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<DevicePublicKeyDto>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }
}
