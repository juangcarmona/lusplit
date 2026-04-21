using System.Text.Json;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.Sync;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Sync;
using NSubstitute;

namespace LuSplit.Application.Tests.Sync;

public sealed class SyncGroupUseCaseTests
{
    private const string GroupId = "group-1";
    private const string DeviceId = "device-1";

    private readonly ISyncPort _syncPort = Substitute.For<ISyncPort>();
    private readonly IOperationRepository _operationRepository = Substitute.For<IOperationRepository>();
    private readonly ISyncCursorRepository _cursorRepository = Substitute.For<ISyncCursorRepository>();
    private readonly ISharedGroupStateRepository _sharedStateRepository = Substitute.For<ISharedGroupStateRepository>();
    private readonly IEncryptionPort _encryption = Substitute.For<IEncryptionPort>();
    private readonly IGroupKeyProvider _keyProvider = Substitute.For<IGroupKeyProvider>();
    private readonly InMemoryQueryRepositories _repos = new();

    private SyncGroupUseCase CreateSut()
    {
        var applicator = new OperationApplicator(_repos, _repos, _repos);
        return new SyncGroupUseCase(
            _syncPort, _operationRepository, _cursorRepository, _sharedStateRepository, _encryption, _keyProvider, applicator);
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_DoesNotCallSyncPort()
    {
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _syncPort.DidNotReceive().RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_PullsAndPushesOperations()
    {
        // Arrange: shared group exists
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);

        _syncPort.RequestSyncTokenAsync(GroupId, DeviceId, Arg.Any<CancellationToken>())
            .Returns(new LuSplit.Contracts.ControlPlane.SyncTokenResponse("sas-token", "container1", DateTimeOffset.UtcNow.AddMinutes(15)));

        _cursorRepository.GetAsync(DeviceId, GroupId, Arg.Any<CancellationToken>())
            .Returns((SyncCursor?)null);

        // No remote blobs
        _syncPort.ListRemoteOperationsAsync("container1", string.Empty, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>() as IReadOnlyList<string>);

        // No pending local ops
        _operationRepository.GetPendingAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Operation>() as IReadOnlyList<Operation>);

        // Act
        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        // Assert: sync token was requested
        await _syncPort.Received(1).RequestSyncTokenAsync(GroupId, DeviceId, Arg.Any<CancellationToken>());
        // Cursor saved (no prior cursor, latestHlc == afterCursor but cursor is null)
        await _cursorRepository.Received(1).SaveAsync(Arg.Any<SyncCursor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RemoteOperation_AppliedAndSaved()
    {
        // Arrange
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);

        _syncPort.RequestSyncTokenAsync(GroupId, DeviceId, Arg.Any<CancellationToken>())
            .Returns(new LuSplit.Contracts.ControlPlane.SyncTokenResponse("sas", "container1", DateTimeOffset.UtcNow.AddMinutes(15)));

        _cursorRepository.GetAsync(DeviceId, GroupId, Arg.Any<CancellationToken>())
            .Returns((SyncCursor?)null);

        var operationId = "op-1";
        var hlc = "2024-01-01T00:00:00Z";
        var blobName = $"{hlc}_{operationId}";

        _syncPort.ListRemoteOperationsAsync("container1", string.Empty, Arg.Any<CancellationToken>())
            .Returns(new[] { blobName } as IReadOnlyList<string>);

        _operationRepository.ExistsAsync(operationId, Arg.Any<CancellationToken>())
            .Returns(false);

        var groupKey = new byte[32];
        _keyProvider.GetGroupKeyAsync(GroupId, DeviceId, 1, Arg.Any<CancellationToken>())
            .Returns(groupKey);

        // Produce a fake operation payload
        var addExpense = new AddExpensePayload("exp1", "Coffee", 5m, "USD", "p1", DateTimeOffset.UtcNow,
            [new SplitLinePayload("p1", 5m)]);

        var operation = new Operation(operationId, GroupId, DeviceId, "user1", hlc,
            OperationType.AddExpense, "exp1", JsonSerializer.SerializeToUtf8Bytes(addExpense), 1, DateTimeOffset.UtcNow);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(operation);

        var envelope = new OperationEnvelope(1, new byte[12], plaintext, new byte[16]);
        var envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope);

        _syncPort.DownloadOperationAsync("container1", blobName, Arg.Any<CancellationToken>())
            .Returns(envelopeBytes);

        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<byte[]>(), groupKey)
            .Returns(plaintext);

        _operationRepository.GetPendingAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Operation>() as IReadOnlyList<Operation>);

        // Act
        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        // Assert: operation was saved and applicator ran (expense appeared in repos)
        await _operationRepository.Received(1).SaveAsync(Arg.Is<Operation>(o => o.OperationId == operationId), Arg.Any<CancellationToken>());
        Assert.Single(_repos.Expenses);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateRemoteOperation_SkippedIdempotently()
    {
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);

        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuSplit.Contracts.ControlPlane.SyncTokenResponse("sas", "container1", DateTimeOffset.UtcNow.AddMinutes(15)));

        _cursorRepository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SyncCursor?)null);

        var operationId = "op-already-applied";
        var blobName = $"2024-01-01T00:00:00Z_{operationId}";

        _syncPort.ListRemoteOperationsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { blobName } as IReadOnlyList<string>);

        // Already exists → idempotency check returns true
        _operationRepository.ExistsAsync(operationId, Arg.Any<CancellationToken>())
            .Returns(true);

        _operationRepository.GetPendingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Operation>() as IReadOnlyList<Operation>);

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        // Blob was not downloaded — skipped entirely
        await _syncPort.DidNotReceive().DownloadOperationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CursorAdvancedAfterSync()
    {
        var sharedState = new SharedGroupState(true, "container1", "owner1", 1, SyncStatus.UpToDate, false);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState);

        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuSplit.Contracts.ControlPlane.SyncTokenResponse("sas", "container1", DateTimeOffset.UtcNow.AddMinutes(15)));

        var existingHlc = "2024-01-01T00:00:00Z";
        _cursorRepository.GetAsync(DeviceId, GroupId, Arg.Any<CancellationToken>())
            .Returns(new SyncCursor(DeviceId, GroupId, existingHlc, DateTimeOffset.UtcNow.AddMinutes(-5)));

        // No remote ops, no pending ops
        _syncPort.ListRemoteOperationsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>() as IReadOnlyList<string>);

        _operationRepository.GetPendingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Operation>() as IReadOnlyList<Operation>);

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        // No new ops → cursor NOT saved (latestHlc == afterCursor AND cursor already exists)
        await _cursorRepository.DidNotReceive().SaveAsync(Arg.Any<SyncCursor>(), Arg.Any<CancellationToken>());
    }
}
