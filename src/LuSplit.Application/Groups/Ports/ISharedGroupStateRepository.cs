using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Ports;

public interface ISharedGroupStateRepository
{
    Task<SharedGroupState?> GetByGroupIdAsync(string groupId, CancellationToken ct);
    Task SaveAsync(string groupId, SharedGroupState state, CancellationToken ct);
}
