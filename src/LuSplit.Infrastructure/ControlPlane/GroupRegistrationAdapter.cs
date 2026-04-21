using System.Net;
using System.Net.Http.Json;
using LuSplit.Application.Groups.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class GroupRegistrationAdapter : IGroupRegistrationPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public GroupRegistrationAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CreateGroupResponse> RegisterGroupAsync(CreateGroupRequest request, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/groups")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(req, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException($"Group {request.GroupId} is already registered.");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreateGroupResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task<GroupInfoResponse> GetGroupInfoAsync(string groupId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/groups/{Uri.EscapeDataString(groupId)}");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GroupInfoResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }
}
