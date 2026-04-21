using Azure.Data.Tables;

namespace LuSplit.Functions.Services;

public interface IInvitationStore
{
    Task EnsureTableExistsAsync(CancellationToken ct);
    Task SaveInvitationAsync(string invitationId, string groupId, string invitedByUserId, string invitedByDeviceId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct);
    Task<TableEntity?> GetInvitationAsync(string groupId, string invitationId, CancellationToken ct);
    Task<TableEntity?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task UpdateStatusAsync(string groupId, string invitationId, string status, CancellationToken ct);
}
