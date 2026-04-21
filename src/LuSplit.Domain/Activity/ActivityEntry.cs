namespace LuSplit.Domain.Activity;

public enum ActivityEntryType
{
    ExpenseAdded,
    ExpenseEdited,
    ExpenseDeleted,
    PaymentRecorded,
    MemberJoined,
    MemberRevoked,
    OwnershipTransferred,
    ConflictResolved,
    KeyRotated
}

public sealed record ActivityEntry(
    string EntryId,
    string GroupId,
    ActivityEntryType EntryType,
    string ActorUserId,
    string? EntityId,
    string? Description,
    DateTimeOffset OccurredAt);
