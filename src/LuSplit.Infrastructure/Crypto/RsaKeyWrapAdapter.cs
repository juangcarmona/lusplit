using System.Security.Cryptography;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.Infrastructure.Crypto;

public sealed class RsaKeyWrapAdapter : IKeyWrapPort
{
    /// <inheritdoc/>
    public byte[] WrapKey(byte[] keyToWrap, byte[] recipientPublicKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(recipientPublicKey, out _);
        return rsa.Encrypt(keyToWrap, RSAEncryptionPadding.OaepSHA256);
    }

    /// <inheritdoc/>
    public byte[] UnwrapKey(byte[] wrappedKey, byte[] devicePrivateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(devicePrivateKey, out _);
        return rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
    }

    // Overloads for internal callers that already have RSA instances
    public byte[] WrapKey(byte[] groupKeyBytes, RSA devicePublicKey)
        => devicePublicKey.Encrypt(groupKeyBytes, RSAEncryptionPadding.OaepSHA256);

    public byte[] UnwrapKey(byte[] wrappedKey, RSA devicePrivateKey)
        => devicePrivateKey.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
}
