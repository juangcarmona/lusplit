using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Invitations.Queries;

public sealed class GetPendingInvitationsQuery
{
    private readonly IInvitationPort _invitationPort;
    private readonly ISharedGroupStateRepository _sharedStateRepository;

    public GetPendingInvitationsQuery(
        IInvitationPort invitationPort,
        ISharedGroupStateRepository sharedStateRepository)
    {
        _invitationPort = invitationPort;
        _sharedStateRepository = sharedStateRepository;
    }

    /// <summary>
    /// Returns pending invitations for the group if the caller is the owner.
    /// Returns an empty list for non-owners.
    /// </summary>
    public async Task<IReadOnlyList<PendingInvitationDto>> ExecuteAsync(
        string groupId,
        string callerUserId,
        CancellationToken ct = default)
    {
        var state = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);
        if (state is null || !string.Equals(state.OwnerId, callerUserId, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PendingInvitationDto>();

        var response = await _invitationPort.ListPendingInvitationsAsync(groupId, callerUserId, ct);
        return response.Invitations;
    }
}
