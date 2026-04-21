namespace LuSplit.Application.Shared.Ports;

/// <summary>
/// Asymmetric key wrap/unwrap using RSA-OAEP.
/// </summary>
public interface IKeyWrapPort
{
    /// <summary>Wraps (encrypts) the given key bytes using the public key.</summary>
    byte[] WrapKey(byte[] keyToWrap, byte[] recipientPublicKey);

    /// <summary>Unwraps (decrypts) wrapped key bytes using the device private key.</summary>
    byte[] UnwrapKey(byte[] wrappedKey, byte[] devicePrivateKey);
}
