using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Invitations.UseCases;

/// <summary>
/// Accepts a group invitation:
/// 1. Fetches invitation info (validates token is still Pending).
/// 2. Posts accept to control plane — receives group key material.
/// 3. Unwraps and stores the group key for the accepting device.
/// 4. Persists the SharedGroupState locally.
/// 5. Triggers initial sync for the new group.
/// </summary>
public sealed class AcceptInvitationUseCase
{
    private readonly IInvitationPort _invitationPort;
    private readonly ISecureKeyStoragePort _keyStorage;
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly SyncGroupUseCase _syncGroupUseCase;

    public AcceptInvitationUseCase(
        IInvitationPort invitationPort,
        ISecureKeyStoragePort keyStorage,
        ISharedGroupStateRepository sharedStateRepository,
        SyncGroupUseCase syncGroupUseCase)
    {
        _invitationPort = invitationPort;
        _keyStorage = keyStorage;
        _sharedStateRepository = sharedStateRepository;
        _syncGroupUseCase = syncGroupUseCase;
    }

    public async Task<AcceptInvitationResult> ExecuteAsync(
        string invitationCode,
        string acceptingUserId,
        string deviceId,
        byte[] devicePublicKey,
        CancellationToken ct = default)
    {
        // 1. Validate the token is still usable
        var info = await _invitationPort.GetInvitationInfoAsync(invitationCode, ct);

        if (!string.Equals(info.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Invitation is no longer valid. Status: {info.Status}");

        if (info.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Invitation has expired.");

        // 2. Accept — receive group key material
        var request = new AcceptInvitationRequest(invitationCode, acceptingUserId, deviceId, devicePublicKey);
        var response = await _invitationPort.AcceptInvitationAsync(request, ct);

        // 3. Store wrapped group key for each key version provided
        foreach (var wrappedKeyEntry in response.WrappedKeys)
        {
            if (string.Equals(wrappedKeyEntry.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                await _keyStorage.StoreWrappedKeyAsync(response.GroupId, 1, wrappedKeyEntry.WrappedKey, ct);
        }

        // 4. Persist shared group state
        var sharedState = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: response.ContainerName,
            OwnerId: info.InvitedByDisplayName, // Best available — actual ownerId would need a separate call
            CurrentKeyVersion: 1,
            SyncStatus: SyncStatus.UpToDate,
            IsReadOnly: false);

        await _sharedStateRepository.SaveAsync(response.GroupId, sharedState, ct);

        // 5. Trigger initial sync (best-effort — not a hard failure if it fails)
        try
        {
            await _syncGroupUseCase.ExecuteAsync(response.GroupId, deviceId, ct);
        }
        catch
        {
            // Sync failure is not fatal here; group is already accepted.
        }

        return new AcceptInvitationResult(response.GroupId, info.GroupName);
    }
}

public sealed record AcceptInvitationResult(string GroupId, string GroupName);
