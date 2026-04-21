using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.Invitations;

public sealed class AcceptInvitationUseCaseTests
{
    private const string GroupId = "group-1";
    private const string InvitationCode = "abc123";
    private const string UserId = "user-1";
    private const string DeviceId = "device-1";

    private readonly IInvitationPort _invitationPort = Substitute.For<IInvitationPort>();
    private readonly ISecureKeyStoragePort _keyStorage = Substitute.For<ISecureKeyStoragePort>();
    private readonly ISharedGroupStateRepository _sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();
    private readonly ISyncPort _syncPort = Substitute.For<ISyncPort>();
    private readonly IOperationRepository _operationRepository = Substitute.For<IOperationRepository>();
    private readonly ISyncCursorRepository _cursorRepository = Substitute.For<ISyncCursorRepository>();
    private readonly IGroupKeyProvider _keyProvider = Substitute.For<IGroupKeyProvider>();
    private readonly IEncryptionPort _encryption = Substitute.For<IEncryptionPort>();
    private readonly InMemoryQueryRepositories _repos = new();

    private AcceptInvitationUseCase CreateSut()
    {
        var applicator = new Application.Sync.OperationApplicator(_repos, _repos, _repos);
        var syncUseCase = new SyncGroupUseCase(
            _syncPort, _operationRepository, _cursorRepository, _sharedStateRepo, _encryption, _keyProvider, applicator);
        return new AcceptInvitationUseCase(_invitationPort, _keyStorage, _sharedStateRepo, syncUseCase);
    }

    private void SetupValidInvitation(string status = "Pending", DateTimeOffset? expiresAt = null)
    {
        _invitationPort.GetInvitationInfoAsync(InvitationCode, Arg.Any<CancellationToken>())
            .Returns(new InvitationInfoResponse(
                "inv-1", GroupId, "Trip to Italy", "Alice",
                expiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
                status));

        _invitationPort.AcceptInvitationAsync(Arg.Any<AcceptInvitationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcceptInvitationResponse(GroupId, "container-1", Array.Empty<WrappedKeyEntryDto>()));

        // Sync will skip (non-shared group initially)
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsGroupIdAndName()
    {
        SetupValidInvitation();

        var result = await CreateSut().ExecuteAsync(
            InvitationCode, UserId, DeviceId, Array.Empty<byte>());

        Assert.Equal(GroupId, result.GroupId);
        Assert.Equal("Trip to Italy", result.GroupName);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CallsAcceptPort()
    {
        SetupValidInvitation();

        await CreateSut().ExecuteAsync(InvitationCode, UserId, DeviceId, Array.Empty<byte>());

        await _invitationPort.Received(1).AcceptInvitationAsync(
            Arg.Is<AcceptInvitationRequest>(r => r.AcceptingUserId == UserId && r.InvitationCode == InvitationCode),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_PersistsSharedGroupState()
    {
        SetupValidInvitation();

        await CreateSut().ExecuteAsync(InvitationCode, UserId, DeviceId, Array.Empty<byte>());

        await _sharedStateRepo.Received(1).SaveAsync(
            GroupId, Arg.Is<SharedGroupState>(s => s.IsShared && s.RemoteContainerName == "container-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredToken_ThrowsInvalidOperationException()
    {
        SetupValidInvitation("Pending", DateTimeOffset.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().ExecuteAsync(InvitationCode, UserId, DeviceId, Array.Empty<byte>()));
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyAcceptedToken_ThrowsInvalidOperationException()
    {
        SetupValidInvitation("Accepted");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().ExecuteAsync(InvitationCode, UserId, DeviceId, Array.Empty<byte>()));
    }

    [Fact]
    public async Task ExecuteAsync_SyncFailure_DoesNotThrow()
    {
        SetupValidInvitation();

        // Make sync throw
        _syncPort.RequestSyncTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<LuSplit.Contracts.ControlPlane.SyncTokenResponse>(_ => throw new HttpRequestException("Sync failed"));

        // The shared state was previously null → sync skips. But let's set it to shared so sync runs.
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new SharedGroupState(true, "container-1", "owner", 1, SyncStatus.UpToDate, false));

        // Re-setup the port calls
        _invitationPort.GetInvitationInfoAsync(InvitationCode, Arg.Any<CancellationToken>())
            .Returns(new InvitationInfoResponse(
                "inv-1", GroupId, "Trip", "Alice",
                DateTimeOffset.UtcNow.AddDays(7), "Pending"));
        _invitationPort.AcceptInvitationAsync(Arg.Any<AcceptInvitationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcceptInvitationResponse(GroupId, "container-1", Array.Empty<WrappedKeyEntryDto>()));

        // Should not throw — sync failure is swallowed
        var result = await CreateSut().ExecuteAsync(InvitationCode, UserId, DeviceId, Array.Empty<byte>());
        Assert.Equal(GroupId, result.GroupId);
    }
}
