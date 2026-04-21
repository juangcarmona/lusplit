using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Identity;

namespace LuSplit.Application.Identity.Ports;

public interface IDeviceRegistrationPort
{
    Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct);
    Task<ListDevicesResponse> ListDevicesAsync(string userId, CancellationToken ct);
    Task RevokeDeviceAsync(string deviceId, string revokedByUserId, CancellationToken ct);
}
