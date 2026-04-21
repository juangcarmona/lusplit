using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.KeyManagement.Ports;

/// <summary>
/// Uploads and retrieves RSA-wrapped group keys from the control plane.
/// </summary>
public interface IKeyRotationPort
{
    /// <summary>Posts a new set of wrapped keys for a new key version.</summary>
    Task UploadRotatedKeyAsync(string groupId, UploadRotatedKeyRequest request, CancellationToken ct);

    /// <summary>Returns all wrapped key versions available for a device.</summary>
    Task<GetWrappedKeysForDeviceResponse> GetWrappedKeysForDeviceAsync(string groupId, string deviceId, CancellationToken ct);

    /// <summary>Returns device public keys for all non-revoked devices in a group.</summary>
    Task<IReadOnlyList<DevicePublicKeyDto>> GetDevicePublicKeysAsync(string groupId, CancellationToken ct);
}

/// <summary>Device public key data needed for RSA wrapping of group key.</summary>
public sealed record DevicePublicKeyDto(string DeviceId, byte[] PublicKey);
