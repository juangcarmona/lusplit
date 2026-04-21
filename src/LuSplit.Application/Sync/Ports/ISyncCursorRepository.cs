using LuSplit.Domain.Sync;

namespace LuSplit.Application.Sync.Ports;

public interface ISyncCursorRepository
{
    Task<SyncCursor?> GetAsync(string deviceId, string groupId, CancellationToken ct);
    Task SaveAsync(SyncCursor cursor, CancellationToken ct);
}
