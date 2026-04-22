using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Tests.Fakes;

internal sealed class InMemoryOperationRepository : IOperationRepository
{
    public List<Operation> SavedOperations { get; } = new();

    public Task SaveAsync(Operation operation, CancellationToken ct)
    {
        SavedOperations.Add(operation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Operation>> GetPendingAsync(string groupId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Operation>>(
            SavedOperations.Where(o => string.Equals(o.GroupId, groupId, StringComparison.Ordinal)).ToList());

    public Task MarkSyncedAsync(IReadOnlyList<string> operationIds, CancellationToken ct)
    {
        SavedOperations.RemoveAll(o => operationIds.Contains(o.OperationId));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string operationId, CancellationToken ct)
        => Task.FromResult(SavedOperations.Any(o => o.OperationId == operationId));
}

internal sealed class InMemorySharedGroupStateRepository : ISharedGroupStateRepository
{
    private readonly Dictionary<string, SharedGroupState> _store = new();

    public Task<SharedGroupState?> GetByGroupIdAsync(string groupId, CancellationToken ct)
    {
        _store.TryGetValue(groupId, out var state);
        return Task.FromResult(state);
    }

    public Task SaveAsync(string groupId, SharedGroupState state, CancellationToken ct)
    {
        _store[groupId] = state;
        return Task.CompletedTask;
    }
}
