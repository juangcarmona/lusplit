using LuSplit.App.Features.Invitations;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class InviteViewModelTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";
    private const string DeviceId = "device-1";

    private static (InviteViewModel vm, IInvitationPort port, IShareSheetPort shareSheet, IClipboardPort clipboard) Build(
        string? invitationCode = "tok-abc",
        Exception? portException = null,
        bool shareSheetResult = true)
    {
        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        var port = Substitute.For<IInvitationPort>();
        var auth = Substitute.For<IAuthPort>();
        var groupRepo = Substitute.For<IGroupRepository>();
        var shareSheet = Substitute.For<IShareSheetPort>();
        var clipboard = Substitute.For<IClipboardPort>();

        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerId);
        stateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new SharedGroupState(true, "container-1", OwnerId, 1, SyncStatus.UpToDate, false));

        if (portException is not null)
            port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(portException);
        else
            port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
                .Returns(new CreateInvitationResponse("inv-1", invitationCode!, DateTimeOffset.UtcNow.AddDays(7)));

        shareSheet.ShareTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(shareSheetResult);

        var useCase = new CreateInvitationUseCase(stateRepo, port, auth, groupRepo);
        var vm = new InviteViewModel(useCase, shareSheet, clipboard);
        vm.Initialize(GroupId, DeviceId);

        return (vm, port, shareSheet, clipboard);
    }

    [Fact]
    public void InitialState_IsLoadingFalse()
    {
        var (vm, _, _, _) = Build();

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void InitialState_ErrorMessageNull()
    {
        var (vm, _, _, _) = Build();

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void InitialState_FlowStateIsInitial()
    {
        var (vm, _, _, _) = Build();

        Assert.Equal(InviteFlowState.Initial, vm.FlowState);
    }

    [Fact]
    public void InitialState_CanInviteIsTrue()
    {
        var (vm, _, _, _) = Build();

        Assert.True(vm.CanInvite);
    }

    // --- InviteCommand: share-first flow (T226) ---

    [Fact]
    public async Task InviteCommand_SetsIsLoadingDuringExecution()
    {
        var (vm, _, _, _) = Build();
        bool wasLoading = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
                wasLoading = true;
        };

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.True(wasLoading);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task InviteCommand_OnSuccess_SetsGeneratedLink()
    {
        var (vm, _, _, _) = Build(invitationCode: "mytoken");

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.NotNull(vm.GeneratedLink);
        Assert.Contains("mytoken", vm.GeneratedLink);
    }

    [Fact]
    public async Task InviteCommand_OnSuccess_CallsShareSheet()
    {
        var (vm, _, shareSheet, _) = Build();

        await vm.InviteCommand.ExecuteAsync(null);

        await shareSheet.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("tok-abc")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteCommand_ShareAccepted_FlowStateIsShareCompleted()
    {
        var (vm, _, _, _) = Build(shareSheetResult: true);

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.Equal(InviteFlowState.ShareCompleted, vm.FlowState);
    }

    [Fact]
    public async Task InviteCommand_ShareCancelled_FlowStateIsShareCancelled()
    {
        var (vm, _, _, _) = Build(shareSheetResult: false);

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.Equal(InviteFlowState.ShareCancelled, vm.FlowState);
    }

    [Fact]
    public async Task InviteCommand_OnError_SetsErrorMessage()
    {
        var (vm, _, _, _) = Build(portException: new Exception("Network error"));

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.GeneratedLink);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task InviteCommand_OnError_FlowStateResetsToInitial()
    {
        var (vm, _, _, _) = Build(portException: new Exception("boom"));

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.Equal(InviteFlowState.Initial, vm.FlowState);
    }

    // --- Fallback actions (T227) ---

    [Fact]
    public async Task ShareCancelled_ShowsFallbackActions()
    {
        var (vm, _, _, _) = Build(shareSheetResult: false);

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.True(vm.ShowFallbackActions);
    }

    [Fact]
    public async Task ShareCompleted_ShowsFallbackActions()
    {
        var (vm, _, _, _) = Build(shareSheetResult: true);

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.True(vm.ShowFallbackActions);
    }

    [Fact]
    public async Task CopyInviteLinkCommand_CopiesLinkToClipboard()
    {
        var (vm, _, _, clipboard) = Build();
        await vm.InviteCommand.ExecuteAsync(null);

        await vm.CopyInviteLinkCommand.ExecuteAsync(null);

        await clipboard.Received(1).SetTextAsync(Arg.Is<string>(s => s.Contains("tok-abc")));
    }

    [Fact]
    public async Task CopyInviteLinkCommand_NoLink_DoesNothing()
    {
        var (vm, _, _, clipboard) = Build();

        await vm.CopyInviteLinkCommand.ExecuteAsync(null);

        await clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ShareAgainCommand_ResharesSameLink()
    {
        var (vm, _, shareSheet, _) = Build();
        await vm.InviteCommand.ExecuteAsync(null);
        shareSheet.ClearReceivedCalls();

        await vm.ShareAgainCommand.ExecuteAsync(null);

        await shareSheet.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("tok-abc")),
            Arg.Any<CancellationToken>());
    }

    // --- Post-create flow ---

    [Fact]
    public void Initialize_PostCreate_SetsIsPostCreate()
    {
        var (vm, _, _, _) = Build();
        vm.Initialize(GroupId, DeviceId, postCreate: true);

        Assert.True(vm.IsPostCreate);
    }

    [Fact]
    public void SkipCommand_RaisesSkipRequested()
    {
        var (vm, _, _, _) = Build();
        vm.Initialize(GroupId, DeviceId, postCreate: true);
        bool raised = false;
        vm.SkipRequested += (_, _) => raised = true;

        vm.SkipCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void DoneCommand_RaisesDoneRequested()
    {
        var (vm, _, _, _) = Build();
        vm.Initialize(GroupId, DeviceId, postCreate: true);
        bool raised = false;
        vm.DoneRequested += (_, _) => raised = true;

        vm.DoneCommand.Execute(null);

        Assert.True(raised);
    }

    // --- No share sheet fallback ---

    [Fact]
    public async Task InviteCommand_NoShareSheet_FlowStateIsLinkReady()
    {
        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        var port = Substitute.For<IInvitationPort>();
        var auth = Substitute.For<IAuthPort>();
        var groupRepo = Substitute.For<IGroupRepository>();

        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerId);
        stateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new SharedGroupState(true, "container-1", OwnerId, 1, SyncStatus.UpToDate, false));
        port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateInvitationResponse("inv-1", "tok-abc", DateTimeOffset.UtcNow.AddDays(7)));

        var useCase = new CreateInvitationUseCase(stateRepo, port, auth, groupRepo);
        var vm = new InviteViewModel(useCase); // No share sheet
        vm.Initialize(GroupId, DeviceId);

        await vm.InviteCommand.ExecuteAsync(null);

        Assert.Equal(InviteFlowState.LinkReady, vm.FlowState);
        Assert.NotNull(vm.GeneratedLink);
    }
}
