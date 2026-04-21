using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Sync;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.Application.Tests.Revocation;

public sealed class OwnerLossReadOnlyTests
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

    private void SetupSharedGroup(bool isReadOnly = false)
    {
        var state = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: "container-1",
            OwnerId: "owner-1",
            CurrentKeyVersion: 1,
            SyncStatus: LuSplit.Domain.Groups.SyncStatus.UpToDate,
            IsReadOnly: isReadOnly);
        _sharedStateRepository.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>()).Returns(state);
    }

    [Fact]
    public async Task SyncGroupUseCase_Receives403_SetsGroupReadOnly()
    {
        SetupSharedGroup();
        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden));

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _sharedStateRepository.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.IsReadOnly == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncGroupUseCase_ReceivesNotFound_SetsGroupReadOnly()
    {
        SetupSharedGroup();
        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound));

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _sharedStateRepository.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.IsReadOnly == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncGroupUseCase_NormalSync_DoesNotSetReadOnly()
    {
        SetupSharedGroup();
        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SyncTokenResponse("sas-token", "container-1", DateTimeOffset.UtcNow.AddHours(1)));
        _syncPort.ListRemoteOperationsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());
        _operationRepository.GetPendingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<LuSplit.Domain.Sync.Operation>() as IReadOnlyList<LuSplit.Domain.Sync.Operation>);

        await CreateSut().ExecuteAsync(GroupId, DeviceId);

        await _sharedStateRepository.DidNotReceive().SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.IsReadOnly == true),
            Arg.Any<CancellationToken>());
    }
}
