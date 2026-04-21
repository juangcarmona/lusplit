using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.Groups.Ports;

public interface IGroupMemberPort
{
    Task<ListMembersResponse> ListMembersAsync(string groupId, CancellationToken ct);
}
