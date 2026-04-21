using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LuSplit.Application.Sync.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Infrastructure.Sync;

/// <summary>
/// Implements blob-level sync operations using Azure Blob Storage.
/// The SAS URI is provided per-container by the control plane.
/// </summary>
public sealed class BlobSyncAdapter : ISyncPort
{
    private readonly SasTokenProvider _sasTokenProvider;
    private static readonly int[] RetryDelaysMs = [500, 1000, 2000];

    public BlobSyncAdapter(SasTokenProvider sasTokenProvider)
    {
        _sasTokenProvider = sasTokenProvider;
    }

    public async Task<IReadOnlyList<string>> ListRemoteOperationsAsync(
        string containerName, string afterHlcCursor, CancellationToken ct)
    {
        var containerClient = await GetContainerClientAsync(containerName, ct);

        var blobs = new List<string>();
        await foreach (var item in containerClient.GetBlobsAsync(prefix: "ops/", cancellationToken: ct))
        {
            var name = item.Name;
            // Blob name is "ops/{hlcTimestamp}_{operationId}"
            var blobKey = name["ops/".Length..];
            if (string.Compare(blobKey, afterHlcCursor, StringComparison.Ordinal) > 0)
                blobs.Add(blobKey);
        }

        blobs.Sort(StringComparer.Ordinal);
        return blobs;
    }

    public async Task<byte[]> DownloadOperationAsync(
        string containerName, string blobName, CancellationToken ct)
    {
        var containerClient = await GetContainerClientAsync(containerName, ct);
        var blobClient = containerClient.GetBlobClient($"ops/{blobName}");

        var response = await blobClient.DownloadContentAsync(ct);
        return response.Value.Content.ToArray();
    }

    public async Task UploadOperationAsync(
        string containerName, string blobName, byte[] encryptedBytes, CancellationToken ct)
    {
        var containerClient = await GetContainerClientAsync(containerName, ct);
        var blobClient = containerClient.GetBlobClient($"ops/{blobName}");

        // Parse KeyVersion from the envelope to write as blob metadata
        var uploadOptions = new BlobUploadOptions();
        try
        {
            var envelope = System.Text.Json.JsonSerializer.Deserialize<LuSplit.Contracts.Sync.OperationEnvelope>(encryptedBytes);
            if (envelope is not null)
            {
                uploadOptions.Metadata = new Dictionary<string, string>
                {
                    ["KeyVersion"] = envelope.KeyVersion.ToString()
                };
            }
        }
        catch { /* Don't fail upload if metadata parsing fails */ }

        using var stream = new MemoryStream(encryptedBytes);
        await blobClient.UploadAsync(stream, uploadOptions, ct);
    }

    public async Task<SyncTokenResponse> RequestSyncTokenAsync(
        string groupId, string deviceId, CancellationToken ct)
        => await _sasTokenProvider.GetTokenAsync(groupId, deviceId, ct);

    public async Task WriteSnapshotAsync(
        string containerName, string snapshotId, byte[] encryptedSnapshotBytes, CancellationToken ct)
    {
        var containerClient = await GetContainerClientAsync(containerName, ct);
        var blobClient = containerClient.GetBlobClient($"snapshots/{snapshotId}");

        using var stream = new MemoryStream(encryptedSnapshotBytes);
        await blobClient.UploadAsync(stream, overwrite: true, ct);
    }

    public async Task<byte[]?> ReadLatestSnapshotAsync(string containerName, CancellationToken ct)
    {
        var containerClient = await GetContainerClientAsync(containerName, ct);

        BlobItem? latest = null;
        await foreach (var item in containerClient.GetBlobsAsync(prefix: "snapshots/", cancellationToken: ct))
        {
            if (latest is null ||
                string.Compare(item.Name, latest.Name, StringComparison.Ordinal) > 0)
                latest = item;
        }

        if (latest is null) return null;

        var blobClient = containerClient.GetBlobClient(latest.Name);
        var response = await blobClient.DownloadContentAsync(ct);
        return response.Value.Content.ToArray();
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken ct)
    {
        var sasUri = await _sasTokenProvider.GetContainerSasUriAsync(containerName, ct);
        return new BlobContainerClient(sasUri);
    }

    private static async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (IsTransient(ex.Status) && attempt < RetryDelaysMs.Length)
            {
                await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }
        return await operation(); // Final attempt — let exception propagate
    }

    private static async Task WithRetryAsync(Func<Task> operation, CancellationToken ct)
    {
        await WithRetryAsync(async () => { await operation(); return true; }, ct);
    }

    private static bool IsTransient(int status) =>
        status == 408 || status == 429 || status >= 500;
}

