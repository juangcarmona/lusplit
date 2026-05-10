using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.UseCases;

/// <summary>
/// Fetches authoritative group info from the control plane and hydrates
/// local shared-group state + owner membership. Call after create/convert
/// or when the local state may be stale.
/// </summary>
public sealed class RefreshSharedGroupContextUseCase
{
    private readonly IGroupRegistrationPort _registrationPort;
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly IGroupMembershipRepository _membershipRepository;

    public RefreshSharedGroupContextUseCase(
        IGroupRegistrationPort registrationPort,
        ISharedGroupStateRepository sharedStateRepository,
        IGroupMembershipRepository membershipRepository)
    {
        _registrationPort = registrationPort;
        _sharedStateRepository = sharedStateRepository;
        _membershipRepository = membershipRepository;
    }

    /// <summary>
    /// Refreshes the local shared-group state from the control plane.
    /// Returns true if the group is confirmed shared; false otherwise.
    /// </summary>
    public async Task<bool> ExecuteAsync(string groupId, CancellationToken ct = default)
    {
        var info = await _registrationPort.GetGroupInfoAsync(groupId, ct);

        var existingState = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);

        var refreshedState = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: existingState?.RemoteContainerName
                ?? $"grp-{groupId.ToLowerInvariant().Replace("-", "")}",
            OwnerId: info.OwnerId,
            CurrentKeyVersion: info.CurrentKeyVersion,
            SyncStatus: existingState?.SyncStatus ?? SyncStatus.UpToDate,
            IsReadOnly: existingState?.IsReadOnly ?? false);

        await _sharedStateRepository.SaveAsync(groupId, refreshedState, ct);

        // Ensure owner membership exists locally
        var members = await _membershipRepository.GetByGroupIdAsync(groupId, ct);
        var hasOwner = members.Any(m =>
            string.Equals(m.UserId, info.OwnerId, StringComparison.OrdinalIgnoreCase)
            && m.Role == MemberRole.Owner);

        if (!hasOwner)
        {
            var ownerMembership = new GroupMembership(
                groupId, info.OwnerId, MemberRole.Owner, info.CreatedAt, false, null);
            await _membershipRepository.UpsertAsync(ownerMembership, ct);
        }

        return true;
    }
}
