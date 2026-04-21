namespace LuSplit.Domain.Groups;

public enum SyncStatus
{
    UpToDate,
    Syncing,
    PendingLocalChanges,
    SyncError,
}
