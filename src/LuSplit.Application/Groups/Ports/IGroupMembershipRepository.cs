using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Ports;

public interface IGroupMembershipRepository
{
    Task<IReadOnlyList<GroupMembership>> GetByGroupIdAsync(string groupId, CancellationToken ct = default);
    Task UpsertAsync(GroupMembership membership, CancellationToken ct = default);
}
