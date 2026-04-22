using System.Text.Json;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Contracts.Sync;
using LuSplit.Domain.Activity;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Sync.UseCases;

/// <summary>
/// Orchestrates a full sync cycle for a shared group:
/// 1. Request a SAS token from the control plane.
/// 2. Pull remote operations after the local cursor.
/// 3. Decrypt and apply each remote operation.
/// 4. Push pending local operations.
/// 5. Advance the cursor.
/// </summary>
public sealed class SyncGroupUseCase
{
    private readonly ISyncPort _syncPort;
    private readonly IOperationRepository _operationRepository;
    private readonly ISyncCursorRepository _cursorRepository;
    private readonly ISharedGroupStateRepository _sharedStateRepository;
    private readonly IEncryptionPort _encryption;
    private readonly IGroupKeyProvider _keyProvider;
    private readonly OperationApplicator _applicator;
    private readonly IActivityEntryPort? _activityPort;
    private readonly IIdGenerator? _idGenerator;
    private readonly IClock? _clock;

    public SyncGroupUseCase(
        ISyncPort syncPort,
        IOperationRepository operationRepository,
        ISyncCursorRepository cursorRepository,
        ISharedGroupStateRepository sharedStateRepository,
        IEncryptionPort encryption,
        IGroupKeyProvider keyProvider,
        OperationApplicator applicator,
        IActivityEntryPort? activityPort = null,
        IIdGenerator? idGenerator = null,
        IClock? clock = null)
    {
        _syncPort = syncPort;
        _operationRepository = operationRepository;
        _cursorRepository = cursorRepository;
        _sharedStateRepository = sharedStateRepository;
        _encryption = encryption;
        _keyProvider = keyProvider;
        _applicator = applicator;
        _activityPort = activityPort;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task ExecuteAsync(string groupId, string deviceId, CancellationToken ct = default)
    {
        var sharedState = await _sharedStateRepository.GetByGroupIdAsync(groupId, ct);
        if (sharedState is null || !sharedState.IsShared)
            return; // Not a shared group — nothing to sync.

        if (sharedState.IsReadOnly)
            return; // Group is read-only (membership revoked) — skip sync to avoid futile retries.

        // 1. Request SAS token — a 403/404 means membership was revoked; mark group read-only.
        LuSplit.Contracts.ControlPlane.SyncTokenResponse tokenResponse;
        try
        {
            tokenResponse = await _syncPort.RequestSyncTokenAsync(groupId, deviceId, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                                              ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var readOnlyState = sharedState with { IsReadOnly = true };
            await _sharedStateRepository.SaveAsync(groupId, readOnlyState, ct);
            return;
        }
        var containerName = tokenResponse.ContainerName;

        // 2. Get current cursor
        var cursor = await _cursorRepository.GetAsync(deviceId, groupId, ct);
        var afterCursor = cursor?.LastSyncedHlcTimestamp ?? string.Empty;

        // 3. Pull remote operations
        var remoteBlobs = await _syncPort.ListRemoteOperationsAsync(containerName, afterCursor, ct);
        var latestHlc = afterCursor;

        foreach (var blobName in remoteBlobs)
        {
            var operationId = BlobNameToOperationId(blobName);
            if (await _operationRepository.ExistsAsync(operationId, ct))
                continue;

            var encryptedBytes = await _syncPort.DownloadOperationAsync(containerName, blobName, ct);
            var envelope = DeserializeEnvelope(encryptedBytes);

            var groupKey = await _keyProvider.GetGroupKeyAsync(groupId, deviceId, envelope.KeyVersion, ct);
            if (groupKey is null)
                continue; // Skip operations for key versions we can't decrypt.

            var plaintext = _encryption.Decrypt(envelope.Ciphertext, envelope.Nonce, envelope.AuthTag, groupKey);
            var operation = JsonSerializer.Deserialize<Operation>(plaintext)
                ?? throw new InvalidOperationException("Failed to deserialize operation.");

            await _applicator.ApplyAsync(operation, ct);
            await _operationRepository.SaveAsync(operation, ct);
            await _operationRepository.MarkSyncedAsync([operation.OperationId], ct);

            // Detect conflicts with pending local operations for the same entity
            if (_activityPort is not null && _idGenerator is not null && _clock is not null)
            {
                var pending = await _operationRepository.GetPendingAsync(groupId, ct);
                foreach (var local in pending)
                {
                    if (!ConflictResolutionPolicy.IsConflict(operation, local))
                        continue;

                    var resolution = ConflictResolutionPolicy.Resolve(operation, local);
                    var losingOp = resolution.WinningOperationId == operation.OperationId ? local : operation;

                    // If local operation lost, mark it synced (drop it)
                    if (resolution.WinningOperationId == operation.OperationId)
                        await _operationRepository.MarkSyncedAsync([losingOp.OperationId], ct);

                    var outcome = resolution.Outcome == ConflictOutcome.DeleteWins ? "delete-wins" : "lww";
                    var entry = new ActivityEntry(
                        _idGenerator.NextId(),
                        groupId,
                        ActivityEntryType.ConflictResolved,
                        operation.UserId,
                        operation.EntityId,
                        $"Conflict resolved ({outcome}): operation {resolution.WinningOperationId} won over {resolution.LosingOperationId}",
                        _clock.UtcNow);

                    await _activityPort.InsertAsync(entry, ct);
                }
            }

            if (string.Compare(operation.HlcTimestamp, latestHlc, StringComparison.Ordinal) > 0)
                latestHlc = operation.HlcTimestamp;
        }

        // 4. Push pending local operations
        var pendingOps = await _operationRepository.GetPendingAsync(groupId, ct);
        var groupKey1 = await _keyProvider.GetGroupKeyAsync(groupId, deviceId, sharedState.CurrentKeyVersion, ct);

        if (groupKey1 is not null)
        {
            foreach (var op in pendingOps)
            {
                var enriched = op with { DeviceId = op.DeviceId == "" ? deviceId : op.DeviceId };
                var plaintext = JsonSerializer.SerializeToUtf8Bytes(enriched);
                var ciphertextWithTag = _encryption.Encrypt(plaintext, groupKey1, out var nonce);
                var authTag = ciphertextWithTag[^16..];
                var ciphertextOnly = ciphertextWithTag[..^16];

                var envelope = new OperationEnvelope(sharedState.CurrentKeyVersion, nonce, ciphertextOnly, authTag);
                var blobBytes = SerializeEnvelope(envelope);

                var blobName = OperationIdToBlobName(op.HlcTimestamp, op.OperationId);
                await _syncPort.UploadOperationAsync(containerName, blobName, blobBytes, ct);
                await _operationRepository.MarkSyncedAsync([op.OperationId], ct);

                if (string.Compare(op.HlcTimestamp, latestHlc, StringComparison.Ordinal) > 0)
                    latestHlc = op.HlcTimestamp;
            }
        }

        // 5. Advance cursor
        if (latestHlc != afterCursor || cursor is null)
        {
            var newCursor = new SyncCursor(deviceId, groupId, latestHlc, DateTimeOffset.UtcNow);
            await _cursorRepository.SaveAsync(newCursor, ct);
        }
    }

    private static string BlobNameToOperationId(string blobName)
    {
        var idx = blobName.IndexOf('_');
        return idx >= 0 ? blobName[(idx + 1)..] : blobName;
    }

    private static string OperationIdToBlobName(string hlcTimestamp, string operationId)
        => $"{hlcTimestamp}_{operationId}";

    private static OperationEnvelope DeserializeEnvelope(byte[] bytes)
        => JsonSerializer.Deserialize<OperationEnvelope>(bytes)
           ?? throw new InvalidOperationException("Failed to deserialize operation envelope.");

    private static byte[] SerializeEnvelope(OperationEnvelope envelope)
        => JsonSerializer.SerializeToUtf8Bytes(envelope);
}
