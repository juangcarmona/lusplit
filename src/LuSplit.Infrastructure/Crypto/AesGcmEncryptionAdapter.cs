using System.Security.Cryptography;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.Infrastructure.Crypto;

public sealed class AesGcmEncryptionAdapter : IEncryptionPort
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public byte[] Encrypt(byte[] plaintext, byte[] key, out byte[] nonce)
    {
        nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[ciphertext.Length + TagSizeBytes];
        ciphertext.CopyTo(result, 0);
        tag.CopyTo(result, ciphertext.Length);
        return result;
    }

    public byte[] Decrypt(byte[] ciphertext, byte[] nonce, byte[] authTag, byte[] key)
    {
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, authTag, plaintext);

        return plaintext;
    }
}
