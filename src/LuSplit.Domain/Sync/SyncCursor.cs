namespace LuSplit.Domain.Sync;

public sealed record SyncCursor(
    string DeviceId,
    string GroupId,
    string LastSyncedHlcTimestamp,
    DateTimeOffset LastSyncedAt);
