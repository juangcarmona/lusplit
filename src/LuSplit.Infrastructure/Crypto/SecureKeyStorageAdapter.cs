using LuSplit.Application.Shared.Ports;
using Microsoft.Maui.Storage;

namespace LuSplit.Infrastructure.Crypto;

public sealed class SecureKeyStorageAdapter : ISecureKeyStoragePort
{
    private static string WrappedKeyKey(string groupId, int keyVersion) =>
        $"wrapped_key_{groupId}_{keyVersion}";

    private static string PrivateKeyKey(string deviceId) =>
        $"private_key_{deviceId}";

    public async Task StoreWrappedKeyAsync(string groupId, int keyVersion, byte[] wrappedKey, CancellationToken ct)
    {
        await SecureStorage.Default.SetAsync(WrappedKeyKey(groupId, keyVersion), Convert.ToBase64String(wrappedKey));
    }

    public async Task<byte[]?> RetrieveWrappedKeyAsync(string groupId, int keyVersion, CancellationToken ct)
    {
        var value = await SecureStorage.Default.GetAsync(WrappedKeyKey(groupId, keyVersion));
        return value is null ? null : Convert.FromBase64String(value);
    }

    public async Task StorePrivateKeyAsync(string deviceId, byte[] privateKeyBytes, CancellationToken ct)
    {
        await SecureStorage.Default.SetAsync(PrivateKeyKey(deviceId), Convert.ToBase64String(privateKeyBytes));
    }

    public async Task<byte[]?> RetrievePrivateKeyAsync(string deviceId, CancellationToken ct)
    {
        var value = await SecureStorage.Default.GetAsync(PrivateKeyKey(deviceId));
        return value is null ? null : Convert.FromBase64String(value);
    }
}
