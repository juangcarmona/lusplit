using System.Security.Cryptography;
using LuSplit.Infrastructure.Crypto;

namespace LuSplit.Infrastructure.Tests.Crypto;

public sealed class AesGcmEncryptionAdapterTests
{
    private readonly AesGcmEncryptionAdapter _adapter = new();

    private static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var key = GenerateKey();
        var plaintext = "Hello, LuSplit!"u8.ToArray();

        var encrypted = _adapter.Encrypt(plaintext, key, out var nonce);
        var ciphertext = encrypted[..^16];
        var authTag = encrypted[^16..];

        var decrypted = _adapter.Decrypt(ciphertext, nonce, authTag, key);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithTamperedAuthTag_ThrowsCryptographicException()
    {
        var key = GenerateKey();
        var plaintext = "Tamper test"u8.ToArray();

        var encrypted = _adapter.Encrypt(plaintext, key, out var nonce);
        var ciphertext = encrypted[..^16];
        var authTag = encrypted[^16..];

        authTag[0] ^= 0xFF; // corrupt the tag

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            _adapter.Decrypt(ciphertext, nonce, authTag, key));
    }

    [Fact]
    public void Encrypt_GeneratesUniqueNoncesEachCall()
    {
        var key = GenerateKey();
        var plaintext = "nonce uniqueness"u8.ToArray();

        _adapter.Encrypt(plaintext, key, out var nonce1);
        _adapter.Encrypt(plaintext, key, out var nonce2);

        Assert.NotEqual(nonce1, nonce2);
    }
}
