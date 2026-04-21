using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.Invitations.Ports;

public interface IInvitationPort
{
    Task<CreateInvitationResponse> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken ct);
    Task CancelInvitationAsync(string groupId, string invitationId, CancellationToken ct);
    Task<InvitationInfoResponse> GetInvitationInfoAsync(string token, CancellationToken ct);
    Task<AcceptInvitationResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task DeclineInvitationAsync(string token, string userId, CancellationToken ct);
    Task<ListPendingInvitationsResponse> ListPendingInvitationsAsync(string groupId, string callerUserId, CancellationToken ct);
}
