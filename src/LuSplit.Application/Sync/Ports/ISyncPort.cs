using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Sync.Ports;

public interface ISyncPort
{
    /// <summary>Lists blob names (operation IDs) uploaded after the given HLC timestamp cursor.</summary>
    Task<IReadOnlyList<string>> ListRemoteOperationsAsync(string containerName, string afterHlcCursor, CancellationToken ct);

    /// <summary>Downloads and returns the raw bytes of a single operation blob.</summary>
    Task<byte[]> DownloadOperationAsync(string containerName, string blobName, CancellationToken ct);

    /// <summary>Uploads a single encrypted operation blob.</summary>
    Task UploadOperationAsync(string containerName, string blobName, byte[] encryptedBytes, CancellationToken ct);

    /// <summary>Requests a short-lived SAS token for the group's container from the control plane.</summary>
    Task<SyncTokenResponse> RequestSyncTokenAsync(string groupId, string deviceId, CancellationToken ct);

    /// <summary>Uploads a JSON snapshot of the current group state.</summary>
    Task WriteSnapshotAsync(string containerName, string snapshotId, byte[] encryptedSnapshotBytes, CancellationToken ct);

    /// <summary>Downloads the latest snapshot bytes, or null if none exists.</summary>
    Task<byte[]?> ReadLatestSnapshotAsync(string containerName, CancellationToken ct);
}
