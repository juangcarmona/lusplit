using LuSplit.Domain.Sync;

namespace LuSplit.Application.Sync.Ports;

public interface IOperationRepository
{
    /// <summary>Saves a local operation to the pending queue.</summary>
    Task SaveAsync(Operation operation, CancellationToken ct);

    /// <summary>Returns all pending (unsynchronized) operations for a group, ordered by HLC timestamp.</summary>
    Task<IReadOnlyList<Operation>> GetPendingAsync(string groupId, CancellationToken ct);

    /// <summary>Marks operations as synchronized by their operation IDs.</summary>
    Task MarkSyncedAsync(IReadOnlyList<string> operationIds, CancellationToken ct);

    /// <summary>Returns whether a given operation ID has already been applied (idempotency check).</summary>
    Task<bool> ExistsAsync(string operationId, CancellationToken ct);
}
