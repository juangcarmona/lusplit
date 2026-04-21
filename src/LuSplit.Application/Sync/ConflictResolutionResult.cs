namespace LuSplit.Application.Sync;

/// <summary>
/// Value type capturing the outcome of resolving a sync conflict between two operations.
/// </summary>
public sealed record ConflictResolutionResult(
    string WinningOperationId,
    string LosingOperationId,
    string AffectedEntityId,
    string Resolution);
