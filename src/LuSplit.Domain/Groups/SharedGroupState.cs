namespace LuSplit.Domain.Groups;

public sealed record SharedGroupState(
    bool IsShared,
    string RemoteContainerName,
    string OwnerId,
    int CurrentKeyVersion,
    SyncStatus SyncStatus,
    bool IsReadOnly);
