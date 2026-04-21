using Azure.Data.Tables;

namespace LuSplit.Functions.Services;

public interface IGroupMetadataStore
{
    Task EnsureTableExistsAsync(CancellationToken ct);
    Task SaveGroupAsync(string groupId, string ownerId, string ownerDeviceId, int keyVersion, IReadOnlyList<LuSplit.Contracts.ControlPlane.WrappedKeyEntryDto> wrappedKeys, CancellationToken ct);
    Task<TableEntity?> GetGroupAsync(string groupId, CancellationToken ct);
    Task SetKeyRotationRequiredAsync(string groupId, CancellationToken ct);
    Task UpdateOwnerAsync(string groupId, string newOwnerId, CancellationToken ct);
}
