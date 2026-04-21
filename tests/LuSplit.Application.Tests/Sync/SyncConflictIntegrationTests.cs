using System.Text.Json;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Contracts.Sync;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Activity;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Sync;
using NSubstitute;

namespace LuSplit.Application.Tests.Sync;

public sealed class SyncConflictIntegrationTests
{
    private const string GroupId = "group-1";
    private const string DeviceId = "device-1";

    private readonly ISyncPort _syncPort = Substitute.For<ISyncPort>();
    private readonly IOperationRepository _operationRepository = Substitute.For<IOperationRepository>();
    private readonly ISyncCursorRepository _cursorRepository = Substitute.For<ISyncCursorRepository>();
    private readonly ISharedGroupStateRepository _sharedStateRepository = Substitute.For<ISharedGroupStateRepository>();
    private readonly IEncryptionPort _encryption = Substitute.For<IEncryptionPort>();
    private readonly IGroupKeyProvider _keyProvider = Substitute.For<IGroupKeyProvider>();
    private readonly IActivityEntryPort _activityPort = Substitute.For<IActivityEntryPort>();
    private readonly SequentialIdGenerator _idGenerator = new();
    private readonly FixedClock _clock = new("2024-06-01T12:00:00Z");
    private readonly InMemoryQueryRepositories _repos = new();

    private SyncGroupUseCase CreateSut() =>
        new(_syncPort, _operationRepository, _cursorRepository, _sharedStateRepository,
            _encryption, _keyProvider,
            new OperationApplicator(_repos, _repos, _repos),
            _activityPort, _idGenerator, _clock);

    private static Operation MakeEditOperation(string id, string entityId, string hlc)
    {
        var payload = new EditExpensePayload(entityId, "Coffee", 10m, "USD", "p1",
            DateTimeOffset.UtcNow, [new SplitLinePayload("p1", 10m)]);
        return new Operation(id, GroupId, "dev-x", "user-x", hlc,
            OperationType.EditExpense, entityId,
            JsonSerializer.SerializeToUtf8Bytes(payload), 1, DateTimeOffset.UtcNow);
    }

    private static Operation MakeDeleteOperation(string id, string entityId, string hlc)
    {
        var payload = new DeleteExpensePayload(entityId);
        return new Operation(id, GroupId, "dev-x", "user-x", hlc,
            OperationType.DeleteExpense, entityId,
            JsonSerializer.SerializeToUtf8Bytes(payload), 1, DateTimeOffset.UtcNow);
    }

    private (byte[] envelopeBytes, byte[] plaintextBytes) BuildEnvelope(Operation op)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(op);
        var envelope = new OperationEnvelope(1, new byte[12], plaintext, new byte[16]);
        return (JsonSerializer.SerializeToUtf8Bytes(envelope), plaintext);
    }

    private void SetupSharedState()
    {
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);
        _syncPort.RequestSyncTokenAsync(GroupId, DeviceId, Arg.Any<CancellationToken>())
            .Returns(new SyncTokenResponse("sas", "container1", DateTimeOffset.UtcNow.AddMinutes(15)));
        _cursorRepository.GetAsync(DeviceId, GroupId, Arg.Any<CancellationToken>())
            .Returns((SyncCursor?)null);
        _keyProvider.GetGroupKeyAsync(GroupId, DeviceId, 1, Arg.Any<CancellationToken>())
            .Returns(new byte[32]);
    }

    [Fact]
    public async Task RemoteEditConflictsWithLocalEdit_LaterHlcWins_ActivityEntryWritten()
    {
        SetupSharedState();

        var remoteOp = MakeEditOperation("remote-op", "expense-1", "2024-01-01T00:00:02Z~0001~dev-b");
        var localOp  = MakeEditOperation("local-op",  "expense-1", "2024-01-01T00:00:01Z~0001~dev-a");

        var blobName = $"{remoteOp.HlcTimestamp}_{remoteOp.OperationId}";
        _syncPort.ListRemoteOperationsAsync("container1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { blobName } as IReadOnlyList<string>);
        _operationRepository.ExistsAsync(remoteOp.OperationId, Arg.Any<CancellationToken>()).Returns(false);

        var (envelopeBytes, plaintextBytes) = BuildEnvelope(remoteOp);
        _syncPort.DownloadOperationAsync("container1", blobName, Arg.Any<CancellationToken>()).Returns(envelopeBytes);
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(plaintextBytes);
        // First call: inside pull loop for conflict detection. Second call: push phase (empty = nothing to push).
        _operationRepository.GetPendingAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<Operation>>(new[] { localOp }),
                Task.FromResult<IReadOnlyList<Operation>>(Array.Empty<Operation>()));

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _activityPort.Received(1).InsertAsync(
            Arg.Is<ActivityEntry>(e => e.EntryType == ActivityEntryType.ConflictResolved && e.EntityId == "expense-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoteDeleteConflictsWithLocalEdit_DeleteWins_ActivityEntryWritten()
    {
        SetupSharedState();

        var remoteDelete = MakeDeleteOperation("remote-delete", "expense-1", "2024-01-01T00:00:01Z~0001~dev-b");
        var localEdit    = MakeEditOperation("local-edit",      "expense-1", "2024-01-01T00:00:02Z~0001~dev-a");

        var blobName = $"{remoteDelete.HlcTimestamp}_{remoteDelete.OperationId}";
        _syncPort.ListRemoteOperationsAsync("container1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { blobName } as IReadOnlyList<string>);
        _operationRepository.ExistsAsync(remoteDelete.OperationId, Arg.Any<CancellationToken>()).Returns(false);

        var (envelopeBytes, plaintextBytes) = BuildEnvelope(remoteDelete);
        _syncPort.DownloadOperationAsync("container1", blobName, Arg.Any<CancellationToken>()).Returns(envelopeBytes);
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(plaintextBytes);
        _operationRepository.GetPendingAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<Operation>>(new[] { localEdit }),
                Task.FromResult<IReadOnlyList<Operation>>(Array.Empty<Operation>()));

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _operationRepository.Received(1).MarkSyncedAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Contains(localEdit.OperationId)),
            Arg.Any<CancellationToken>());
        await _activityPort.Received(1).InsertAsync(
            Arg.Is<ActivityEntry>(e => e.EntryType == ActivityEntryType.ConflictResolved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoConflict_DifferentEntities_NoActivityEntry()
    {
        SetupSharedState();

        var remoteOp = MakeEditOperation("remote-op", "expense-1", "2024-01-01T00:00:01Z~0001~dev-b");
        var localOp  = MakeEditOperation("local-op",  "expense-2", "2024-01-01T00:00:00Z~0001~dev-a");

        var blobName = $"{remoteOp.HlcTimestamp}_{remoteOp.OperationId}";
        _syncPort.ListRemoteOperationsAsync("container1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { blobName } as IReadOnlyList<string>);
        _operationRepository.ExistsAsync(remoteOp.OperationId, Arg.Any<CancellationToken>()).Returns(false);

        var (envelopeBytes, plaintextBytes) = BuildEnvelope(remoteOp);
        _syncPort.DownloadOperationAsync("container1", blobName, Arg.Any<CancellationToken>()).Returns(envelopeBytes);
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(plaintextBytes);
        _operationRepository.GetPendingAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<Operation>>(new[] { localOp }),
                Task.FromResult<IReadOnlyList<Operation>>(Array.Empty<Operation>()));

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _activityPort.DidNotReceive().InsertAsync(Arg.Any<ActivityEntry>(), Arg.Any<CancellationToken>());
    }
}
