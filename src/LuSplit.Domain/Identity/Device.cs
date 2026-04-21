namespace LuSplit.Domain.Identity;

public sealed record Device(
    string DeviceId,
    string UserId,
    string DeviceName,
    byte[] PublicKey,
    DateTimeOffset RegisteredAt,
    bool IsRevoked);
