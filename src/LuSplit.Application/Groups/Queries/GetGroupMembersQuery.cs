using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Queries;

public sealed record GroupMemberModel(
    string UserId,
    string DisplayName,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    bool IsOwner);

public sealed class GetGroupMembersQuery
{
    private readonly IGroupMembershipRepository _membershipRepository;

    public GetGroupMembersQuery(IGroupMembershipRepository membershipRepository)
    {
        _membershipRepository = membershipRepository;
    }

    public async Task<IReadOnlyList<GroupMemberModel>> ExecuteAsync(
        string groupId,
        string ownerId,
        IReadOnlyDictionary<string, string>? displayNames = null,
        CancellationToken ct = default)
    {
        var memberships = await _membershipRepository.GetByGroupIdAsync(groupId, ct);
        return memberships
            .Select(m => new GroupMemberModel(
                UserId: m.UserId,
                DisplayName: displayNames?.TryGetValue(m.UserId, out var name) == true ? name : m.UserId,
                Role: m.Role,
                JoinedAt: m.JoinedAt,
                IsOwner: string.Equals(m.UserId, ownerId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
