using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.Groups.Ports;

public interface IGroupRegistrationPort
{
    Task<CreateGroupResponse> RegisterGroupAsync(CreateGroupRequest request, CancellationToken ct);
    Task<GroupInfoResponse> GetGroupInfoAsync(string groupId, CancellationToken ct);
}
