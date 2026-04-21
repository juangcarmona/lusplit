namespace LuSplit.Application.Sync.Ports;

/// <summary>
/// Provides the raw (unwrapped) AES-256 group key for encrypt/decrypt operations.
/// Implementations retrieve the RSA-wrapped key and device private key, then unwrap.
/// </summary>
public interface IGroupKeyProvider
{
    /// <summary>
    /// Returns the raw AES-256 group key for the given group and key version,
    /// unwrapped using the current device's RSA private key.
    /// Returns null if the key is not available on this device.
    /// </summary>
    Task<byte[]?> GetGroupKeyAsync(string groupId, string deviceId, int keyVersion, CancellationToken ct);
}
