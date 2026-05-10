using System.Net.Http.Json;
using LuSplit.Application.Revocation.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class MemberRevocationAdapter : IRevocationPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public MemberRevocationAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task RevokeMemberAsync(string groupId, string memberUserId, string callerUserId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/groups/{Uri.EscapeDataString(groupId)}/members/{Uri.EscapeDataString(memberUserId)}/revoke")
        {
            Content = JsonContent.Create(new RevokeMemberRequest(memberUserId, callerUserId), options: ControlPlaneJsonOptions.Value)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task TransferOwnershipAsync(string groupId, string newOwnerUserId, string callerUserId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/groups/{Uri.EscapeDataString(groupId)}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest(newOwnerUserId, callerUserId), options: ControlPlaneJsonOptions.Value)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }
}
