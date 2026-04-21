using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Sync.Queries;

public sealed class GetSyncStatusQuery
{
    private readonly ISharedGroupStateRepository _sharedStateRepository;

    public GetSyncStatusQuery(ISharedGroupStateRepository sharedStateRepository)
    {
        _sharedStateRepository = sharedStateRepository;
    }

    /// <summary>Returns the sync status for the given group, or null if the group is not a shared group.</summary>
    public async Task<SyncStatus?> ExecuteAsync(string groupId, CancellationToken ct = default)
    {
        var state = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);
        if (state is null || !state.IsShared)
            return null;
        return state.SyncStatus;
    }
}
