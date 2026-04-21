namespace LuSplit.Application.Shared.Ports;

public interface IEncryptionPort
{
    byte[] Encrypt(byte[] plaintext, byte[] key, out byte[] nonce);
    byte[] Decrypt(byte[] ciphertext, byte[] nonce, byte[] authTag, byte[] key);
}
