using System.Security.Cryptography;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Infrastructure.Crypto;

namespace LuSplit.Infrastructure.Sync;

/// <summary>
/// Retrieves and unwraps the raw AES-256 group key for a given key version.
/// </summary>
public sealed class GroupKeyProvider : IGroupKeyProvider
{
    private readonly ISecureKeyStoragePort _keyStorage;
    private readonly RsaKeyWrapAdapter _rsaKeyWrap;

    public GroupKeyProvider(ISecureKeyStoragePort keyStorage, RsaKeyWrapAdapter rsaKeyWrap)
    {
        _keyStorage = keyStorage;
        _rsaKeyWrap = rsaKeyWrap;
    }

    public async Task<byte[]?> GetGroupKeyAsync(string groupId, string deviceId, int keyVersion, CancellationToken ct)
    {
        var wrappedKey = await _keyStorage.RetrieveWrappedKeyAsync(groupId, keyVersion, ct);
        if (wrappedKey is null) return null;

        var privateKeyBytes = await _keyStorage.RetrievePrivateKeyAsync(deviceId, ct);
        if (privateKeyBytes is null) return null;

        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        return _rsaKeyWrap.UnwrapKey(wrappedKey, rsa);
    }
}
