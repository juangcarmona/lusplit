using LuSplit.App.Features.Invitations;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Errors;
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

    private static (InviteViewModel vm, IInvitationPort port) Build(
        string? invitationCode = "tok-abc",
        Exception? portException = null)
    {
        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        var port = Substitute.For<IInvitationPort>();
        var auth = Substitute.For<IAuthPort>();
        var groupRepo = Substitute.For<IGroupRepository>();

        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerId);
        stateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new SharedGroupState(true, "container-1", OwnerId, 1, SyncStatus.UpToDate, false));

        if (portException is not null)
            port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(portException);
        else
            port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
                .Returns(new CreateInvitationResponse("inv-1", invitationCode!, DateTimeOffset.UtcNow.AddDays(7)));

        var useCase = new CreateInvitationUseCase(stateRepo, port, auth, groupRepo);
        var vm = new InviteViewModel(useCase);
        vm.Initialize(GroupId, DeviceId);

        return (vm, port);
    }

    [Fact]
    public async Task GenerateInviteLinkCommand_SetsIsLoadingDuringExecution()
    {
        var (vm, _) = Build();
        bool wasLoading = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
                wasLoading = true;
        };

        await vm.GenerateInviteLinkCommand.ExecuteAsync(null);

        Assert.True(wasLoading);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task GenerateInviteLinkCommand_OnSuccess_SetsGeneratedLink()
    {
        var (vm, _) = Build(invitationCode: "mytoken");

        await vm.GenerateInviteLinkCommand.ExecuteAsync(null);

        Assert.NotNull(vm.GeneratedLink);
        Assert.Contains("mytoken", vm.GeneratedLink);
    }

    [Fact]
    public async Task GenerateInviteLinkCommand_OnSuccess_RaisesInvitationLinkReady()
    {
        var (vm, _) = Build();
        string? raisedLink = null;
        vm.InvitationLinkReady += (_, link) => raisedLink = link;

        await vm.GenerateInviteLinkCommand.ExecuteAsync(null);

        Assert.NotNull(raisedLink);
    }

    [Fact]
    public async Task GenerateInviteLinkCommand_OnError_SetsErrorMessage()
    {
        var (vm, _) = Build(portException: new Exception("Network error"));

        await vm.GenerateInviteLinkCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.GeneratedLink);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void InitialState_IsLoadingFalse()
    {
        var (vm, _) = Build();

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void InitialState_ErrorMessageNull()
    {
        var (vm, _) = Build();

        Assert.Null(vm.ErrorMessage);
    }
}
