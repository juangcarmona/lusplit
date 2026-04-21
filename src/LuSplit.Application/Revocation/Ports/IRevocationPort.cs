using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.Revocation.Ports;

public interface IRevocationPort
{
    Task RevokeMemberAsync(string groupId, string memberUserId, string callerUserId, CancellationToken ct);
    Task TransferOwnershipAsync(string groupId, string newOwnerUserId, string callerUserId, CancellationToken ct);
}
