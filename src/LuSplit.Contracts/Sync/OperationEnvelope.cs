namespace LuSplit.Contracts.Sync;

/// <summary>
/// Wire format envelope for a single encrypted operation blob.
/// Layout: [KeyVersion (4 bytes)] [Nonce (12 bytes)] [Ciphertext (variable)] [AuthTag (16 bytes)]
/// </summary>
public sealed record OperationEnvelope(
    int KeyVersion,
    byte[] Nonce,
    byte[] Ciphertext,
    byte[] AuthTag);
