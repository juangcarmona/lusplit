using System.Net.Http.Json;
using LuSplit.Application.Identity.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class DeviceRegistrationAdapter : IDeviceRegistrationPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public DeviceRegistrationAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/devices/register")
        {
            Content = JsonContent.Create(request, options: ControlPlaneJsonOptions.Value)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>(ControlPlaneJsonOptions.Value, ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task<ListDevicesResponse> ListDevicesAsync(string userId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"api/devices?userId={Uri.EscapeDataString(userId)}");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ListDevicesResponse>(ControlPlaneJsonOptions.Value, ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task RevokeDeviceAsync(string deviceId, string revokedByUserId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"api/devices/{Uri.EscapeDataString(deviceId)}/revoke")
        {
            Content = JsonContent.Create(new { RevokedByUserId = revokedByUserId }, options: ControlPlaneJsonOptions.Value)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }
}
