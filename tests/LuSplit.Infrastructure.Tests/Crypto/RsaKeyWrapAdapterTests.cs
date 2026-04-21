using System.Security.Cryptography;
using LuSplit.Infrastructure.Crypto;

namespace LuSplit.Infrastructure.Tests.Crypto;

public sealed class RsaKeyWrapAdapterTests
{
    private readonly RsaKeyWrapAdapter _adapter = new();

    private static RSA GenerateKeyPair() => RSA.Create(2048);

    [Fact]
    public void WrapKey_ThenUnwrapKey_ReturnsOriginalGroupKey()
    {
        using var rsa = GenerateKeyPair();
        var groupKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = _adapter.WrapKey(groupKey, rsa);
        var unwrapped = _adapter.UnwrapKey(wrapped, rsa);

        Assert.Equal(groupKey, unwrapped);
    }

    [Fact]
    public void UnwrapKey_WithWrongPrivateKey_ThrowsCryptographicException()
    {
        using var correctRsa = GenerateKeyPair();
        using var wrongRsa = GenerateKeyPair();
        var groupKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = _adapter.WrapKey(groupKey, correctRsa);

        Assert.ThrowsAny<CryptographicException>(() =>
            _adapter.UnwrapKey(wrapped, wrongRsa));
    }
}
