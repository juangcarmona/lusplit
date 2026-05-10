using System.Net.Http.Json;
using LuSplit.Application.Groups.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class GroupMemberAdapter : IGroupMemberPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public GroupMemberAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ListMembersResponse> ListMembersAsync(string groupId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/groups/{Uri.EscapeDataString(groupId)}/members");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ListMembersResponse>(ControlPlaneJsonOptions.Value, ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }
}
