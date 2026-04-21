using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Sync.Ports;
using LuSplit.Application.Sync.UseCases;
using LuSplit.App.Features.Invitations;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.App.Tests;

public sealed class InvitationLandingViewModelTests
{
    private const string InvitationCode = "invite-abc";
    private const string UserId = "user-x";
    private const string GroupId = "group-y";

    private readonly IInvitationPort _invitationPort = Substitute.For<IInvitationPort>();
    private readonly ISecureKeyStoragePort _keyStorage = Substitute.For<ISecureKeyStoragePort>();
    private readonly ISharedGroupStateRepository _sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();
    private readonly ISyncPort _syncPort = Substitute.For<ISyncPort>();
    private readonly IOperationRepository _operationRepo = Substitute.For<IOperationRepository>();
    private readonly ISyncCursorRepository _cursorRepo = Substitute.For<ISyncCursorRepository>();
    private readonly IGroupKeyProvider _keyProvider = Substitute.For<IGroupKeyProvider>();
    private readonly IEncryptionPort _encryption = Substitute.For<IEncryptionPort>();
    private readonly LuSplit.Application.Tests.Fakes.InMemoryQueryRepositories _repos = new();
    private readonly IAuthPort _authPort = Substitute.For<IAuthPort>();

    private InvitationLandingViewModel CreateSut()
    {
        var applicator = new OperationApplicator(_repos, _repos, _repos);
        var syncUseCase = new SyncGroupUseCase(_syncPort, _operationRepo, _cursorRepo, _sharedStateRepo, _encryption, _keyProvider, applicator);
        var acceptUseCase = new AcceptInvitationUseCase(_invitationPort, _keyStorage, _sharedStateRepo, syncUseCase);
        var declineUseCase = new DeclineInvitationUseCase(_invitationPort, _authPort);

        var vm = new InvitationLandingViewModel(acceptUseCase, declineUseCase, _authPort, "test-device");
        vm.Initialize(InvitationCode);
        return vm;
    }

    private void SetupValidInvitation()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);

        _invitationPort.GetInvitationInfoAsync(InvitationCode, Arg.Any<CancellationToken>())
            .Returns(new InvitationInfoResponse("inv-1", GroupId, "Trip", "Alice",
                DateTimeOffset.UtcNow.AddDays(7), "Pending"));

        _invitationPort.AcceptInvitationAsync(Arg.Any<AcceptInvitationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcceptInvitationResponse(GroupId, "container-1", Array.Empty<WrappedKeyEntryDto>()));

        // Sync will be skipped (group has no shared state yet)
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);
    }

    [Fact]
    public async Task AcceptCommand_SignsInBeforeCallingUseCase()
    {
        SetupValidInvitation();
        var vm = CreateSut();

        await vm.AcceptCommand.ExecuteAsync(null);

        await _authPort.Received(1).SignInAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptCommand_RaisesAcceptCompletedWithGroupId()
    {
        SetupValidInvitation();
        var vm = CreateSut();

        string? receivedGroupId = null;
        vm.AcceptCompleted += (_, gid) => receivedGroupId = gid;

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(GroupId, receivedGroupId);
    }

    [Fact]
    public async Task AcceptCommand_SetsIsLoadingFalseAfterSuccess()
    {
        SetupValidInvitation();
        var vm = CreateSut();

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task AcceptCommand_OnExpiredInvitation_SetsErrorMessage()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);
        _invitationPort.GetInvitationInfoAsync(InvitationCode, Arg.Any<CancellationToken>())
            .Returns(new InvitationInfoResponse("inv-1", GroupId, "Trip", "Alice",
                DateTimeOffset.UtcNow.AddDays(-1), "Pending")); // expired

        var vm = CreateSut();
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task DeclineCommand_RaisesDeclineCompleted()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);

        var vm = CreateSut();
        bool raised = false;
        vm.DeclineCompleted += (_, _) => raised = true;

        await vm.DeclineCommand.ExecuteAsync(null);

        Assert.True(raised);
    }

    [Fact]
    public async Task DeclineCommand_CallsDeclinePortWithCode()
    {
        _authPort.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(UserId);

        var vm = CreateSut();
        await vm.DeclineCommand.ExecuteAsync(null);

        await _invitationPort.Received(1).DeclineInvitationAsync(InvitationCode, UserId, Arg.Any<CancellationToken>());
    }
}
