namespace LuSplit.Domain.Sync;

public sealed record Operation(
    string OperationId,
    string GroupId,
    string DeviceId,
    string UserId,
    string HlcTimestamp,
    OperationType OperationType,
    string EntityId,
    byte[] EncryptedPayload,
    int KeyVersion,
    DateTimeOffset CreatedAt);
