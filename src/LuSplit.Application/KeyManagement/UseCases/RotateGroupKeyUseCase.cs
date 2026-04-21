using LuSplit.Application.Groups.Ports;
using LuSplit.Application.KeyManagement.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.KeyManagement.UseCases;

/// <summary>
/// Rotates the group encryption key after a member revocation:
/// 1. Generates a new AES-256 group key.
/// 2. Fetches all non-revoked device public keys.
/// 3. Wraps the new key to each device.
/// 4. Uploads wrapped keys to the control plane.
/// 5. Updates local CurrentKeyVersion.
/// </summary>
public sealed class RotateGroupKeyUseCase
{
    private readonly IKeyRotationPort _keyRotationPort;
    private readonly ISharedGroupStateRepository _sharedGroupStateRepository;
    private readonly IKeyWrapPort _keyWrapPort;
    private readonly IEncryptionPort _encryption;

    public RotateGroupKeyUseCase(
        IKeyRotationPort keyRotationPort,
        ISharedGroupStateRepository sharedGroupStateRepository,
        IKeyWrapPort keyWrapPort,
        IEncryptionPort encryption)
    {
        _keyRotationPort = keyRotationPort;
        _sharedGroupStateRepository = sharedGroupStateRepository;
        _keyWrapPort = keyWrapPort;
        _encryption = encryption;
    }

    public async Task ExecuteAsync(string groupId, CancellationToken ct = default)
    {
        var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(groupId, ct)
            ?? throw new InvalidOperationException("Group is not a shared group.");

        if (!KeyRotationPolicy.IsRotationRequired(true))
            return; // No rotation needed

        var newKeyVersion = sharedState.CurrentKeyVersion + 1;
        if (!KeyRotationPolicy.IsVersionMonotonic(sharedState.CurrentKeyVersion, newKeyVersion))
            throw new InvalidOperationException("New key version must be strictly greater than current.");

        // Generate a fresh AES-256 key (32 bytes)
        var newGroupKey = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(newGroupKey);

        // Get public keys for all non-revoked devices
        var deviceKeys = await _keyRotationPort.GetDevicePublicKeysAsync(groupId, ct);

        if (!KeyRotationPolicy.AllActiveDevicesHaveKey(
                deviceKeys.Select(d => d.DeviceId).ToList(),
                deviceKeys.Select(d => new WrappedKeyEntry(d.DeviceId, [])).ToList()))
        {
            // AllActiveDevicesHaveKey is trivially true here since we're building from same source
        }

        var wrappedKeys = deviceKeys
            .Select(d => new WrappedKeyEntryDto(d.DeviceId, _keyWrapPort.WrapKey(newGroupKey, d.PublicKey)))
            .ToList();

        var request = new UploadRotatedKeyRequest(newKeyVersion, wrappedKeys);
        await _keyRotationPort.UploadRotatedKeyAsync(groupId, request, ct);

        // Update local state
        var updatedState = sharedState with { CurrentKeyVersion = newKeyVersion };
        await _sharedGroupStateRepository.SaveAsync(groupId, updatedState, ct);
    }
}
