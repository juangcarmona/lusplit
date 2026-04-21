using System.Net.Http.Json;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class InvitationAdapter : IInvitationPort
{
    private readonly ControlPlaneHttpClient _httpClient;

    public InvitationAdapter(ControlPlaneHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CreateInvitationResponse> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"api/groups/{Uri.EscapeDataString(request.GroupId)}/invitations")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreateInvitationResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task CancelInvitationAsync(string groupId, string invitationId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete,
            $"api/groups/{Uri.EscapeDataString(groupId)}/invitations/{Uri.EscapeDataString(invitationId)}");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<InvitationInfoResponse> GetInvitationInfoAsync(string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"api/invitations/{Uri.EscapeDataString(token)}/info");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<InvitationInfoResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task<AcceptInvitationResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"api/invitations/{Uri.EscapeDataString(request.InvitationCode)}/accept")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AcceptInvitationResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }

    public async Task DeclineInvitationAsync(string token, string userId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"api/invitations/{Uri.EscapeDataString(token)}/decline")
        {
            Content = JsonContent.Create(new { Token = token, UserId = userId })
        };

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ListPendingInvitationsResponse> ListPendingInvitationsAsync(string groupId, string callerUserId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"api/groups/{Uri.EscapeDataString(groupId)}/invitations/pending?userId={Uri.EscapeDataString(callerUserId)}");

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ListPendingInvitationsResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from control plane.");
    }
}
