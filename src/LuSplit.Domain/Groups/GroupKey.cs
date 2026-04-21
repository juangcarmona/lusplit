namespace LuSplit.Domain.Groups;

public sealed record WrappedKeyEntry(string DeviceId, byte[] WrappedKey);

public sealed record GroupKey(
    int KeyVersion,
    DateTimeOffset CreatedAt,
    string CreatedByDeviceId,
    IReadOnlyList<WrappedKeyEntry> WrappedKeys);
