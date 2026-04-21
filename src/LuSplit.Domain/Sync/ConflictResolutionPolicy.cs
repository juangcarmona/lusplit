namespace LuSplit.Domain.Sync;

/// <summary>
/// Outcome of conflict resolution between two competing operations.
/// </summary>
public enum ConflictOutcome
{
    LaterWins,
    DeleteWins
}

/// <summary>
/// Result of resolving a conflict between two operations on the same entity.
/// </summary>
public sealed record ConflictResolutionResult(
    string WinningOperationId,
    string LosingOperationId,
    string AffectedEntityId,
    ConflictOutcome Outcome);

/// <summary>
/// Stateless rules for deterministic conflict resolution between concurrent operations.
/// Rules (in priority order):
///   1. Delete wins over any edit regardless of HLC timestamp.
///   2. For two edits, the operation with the lexicographically later HLC timestamp wins (LWW).
/// Operations on different entities never conflict.
/// Additions are commutative and never conflict.
/// </summary>
public static class ConflictResolutionPolicy
{
    private static readonly OperationType[] DeleteTypes =
    [
        OperationType.DeleteExpense,
        OperationType.DeletePayment,
        OperationType.DeleteTransfer
    ];

    private static readonly OperationType[] AddTypes =
    [
        OperationType.AddExpense,
        OperationType.AddParticipant,
        OperationType.RecordPayment,
        OperationType.AddTransfer
    ];

    /// <summary>Returns true when two operations represent a true write conflict requiring resolution.</summary>
    public static bool IsConflict(Operation a, Operation b)
    {
        if (a.GroupId != b.GroupId || a.EntityId != b.EntityId)
            return false;

        // Two additions to different entity IDs are commutative — no conflict.
        if (AddTypes.Contains(a.OperationType) && AddTypes.Contains(b.OperationType))
            return false;

        // Identical operations — duplicate, not a conflict.
        if (a.OperationId == b.OperationId)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves a conflict between two operations on the same entity.
    /// Assumes <see cref="IsConflict"/> returned true.
    /// </summary>
    public static ConflictResolutionResult Resolve(Operation a, Operation b)
    {
        // Delete-wins
        bool aIsDelete = DeleteTypes.Contains(a.OperationType);
        bool bIsDelete = DeleteTypes.Contains(b.OperationType);

        if (aIsDelete && !bIsDelete)
            return new ConflictResolutionResult(a.OperationId, b.OperationId, a.EntityId, ConflictOutcome.DeleteWins);
        if (bIsDelete && !aIsDelete)
            return new ConflictResolutionResult(b.OperationId, a.OperationId, b.EntityId, ConflictOutcome.DeleteWins);

        // LWW by HLC
        var winner = string.Compare(a.HlcTimestamp, b.HlcTimestamp, StringComparison.Ordinal) >= 0 ? a : b;
        var loser  = ReferenceEquals(winner, a) ? b : a;
        return new ConflictResolutionResult(winner.OperationId, loser.OperationId, winner.EntityId, ConflictOutcome.LaterWins);
    }
}
