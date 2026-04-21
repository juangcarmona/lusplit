using LuSplit.Application.Groups.Ports;
using LuSplit.Application.KeyManagement.UseCases;
using LuSplit.Application.Revocation.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Activity;

namespace LuSplit.Application.Revocation.UseCases;

/// <summary>
/// Revokes a group member:
/// 1. Verifies the caller is the group owner.
/// 2. Calls the control plane to revoke the member.
/// 3. Triggers key rotation (T137).
/// 4. Writes a MemberRevoked activity entry locally.
/// </summary>
public sealed class RevokeMemberUseCase
{
    private readonly IRevocationPort _revocationPort;
    private readonly ISharedGroupStateRepository _sharedGroupStateRepository;
    private readonly IActivityEntryPort _activityEntryPort;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly RotateGroupKeyUseCase? _rotateGroupKeyUseCase;

    public RevokeMemberUseCase(
        IRevocationPort revocationPort,
        ISharedGroupStateRepository sharedGroupStateRepository,
        IActivityEntryPort activityEntryPort,
        IIdGenerator idGenerator,
        IClock clock,
        RotateGroupKeyUseCase? rotateGroupKeyUseCase = null)
    {
        _revocationPort = revocationPort;
        _sharedGroupStateRepository = sharedGroupStateRepository;
        _activityEntryPort = activityEntryPort;
        _idGenerator = idGenerator;
        _clock = clock;
        _rotateGroupKeyUseCase = rotateGroupKeyUseCase;
    }

    public async Task ExecuteAsync(
        string groupId,
        string memberUserIdToRevoke,
        string callerUserId,
        CancellationToken ct = default)
    {
        var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(groupId, ct)
            ?? throw new InvalidOperationException("Group is not a shared group.");

        if (!string.Equals(sharedState.OwnerId, callerUserId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the group owner can revoke members.");

        if (string.Equals(memberUserIdToRevoke, callerUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The owner cannot revoke themselves.");

        await _revocationPort.RevokeMemberAsync(groupId, memberUserIdToRevoke, callerUserId, ct);

        // T137: rotate key after revocation
        if (_rotateGroupKeyUseCase is not null)
            await _rotateGroupKeyUseCase.ExecuteAsync(groupId, ct);
        var entry = new ActivityEntry(
            EntryId: _idGenerator.NextId(),
            GroupId: groupId,
            EntryType: ActivityEntryType.MemberRevoked,
            ActorUserId: callerUserId,
            EntityId: memberUserIdToRevoke,
            Description: null,
            OccurredAt: _clock.UtcNow);

        await _activityEntryPort.InsertAsync(entry, ct);
    }
}
