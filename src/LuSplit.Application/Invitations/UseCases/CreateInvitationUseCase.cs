using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Invitations.UseCases;

public sealed class CreateInvitationUseCase
{
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly IInvitationPort _invitationPort;
    private readonly IAuthPort _authPort;
    private readonly IGroupRepository _groupRepository;

    public CreateInvitationUseCase(
        ISharedGroupStateRepository sharedStateRepository,
        IInvitationPort invitationPort,
        IAuthPort authPort,
        IGroupRepository groupRepository)
    {
        _sharedStateRepository = sharedStateRepository;
        _invitationPort = invitationPort;
        _authPort = authPort;
        _groupRepository = groupRepository;
    }

    /// <summary>
    /// Creates an invitation for a shared group. Only the group owner may invite.
    /// Returns the invitation response containing a short-lived token/link.
    /// </summary>
    public async Task<CreateInvitationResponse> ExecuteAsync(
        string groupId,
        string deviceId,
        CancellationToken ct = default)
    {
        var userId = await _authPort.GetCurrentUserIdAsync(ct)
            ?? throw new ValidationError("User must be signed in to invite members.");

        var sharedState = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);
        if (sharedState is null || !sharedState.IsShared)
            throw new ValidationError($"Group '{groupId}' is not a shared group.");

        if (sharedState.OwnerId != userId)
            throw new ValidationError("Only the group owner may invite members.");

        var request = new CreateInvitationRequest(
            GroupId: groupId,
            InvitedByUserId: userId,
            InvitedByDeviceId: deviceId);

        return await _invitationPort.CreateInvitationAsync(request, ct);
    }
}
