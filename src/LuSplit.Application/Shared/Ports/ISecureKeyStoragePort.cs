namespace LuSplit.Application.Shared.Ports;

public interface ISecureKeyStoragePort
{
    Task StoreWrappedKeyAsync(string groupId, int keyVersion, byte[] wrappedKey, CancellationToken ct);
    Task<byte[]?> RetrieveWrappedKeyAsync(string groupId, int keyVersion, CancellationToken ct);
    Task StorePrivateKeyAsync(string deviceId, byte[] privateKeyBytes, CancellationToken ct);
    Task<byte[]?> RetrievePrivateKeyAsync(string deviceId, CancellationToken ct);
}
