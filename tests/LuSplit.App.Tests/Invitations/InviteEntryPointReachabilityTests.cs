using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Payments.Models;
using LuSplit.Application.Shared.Ports;
using NSubstitute;

namespace LuSplit.App.Tests.Invitations;

public sealed class InviteEntryPointReachabilityTests
{
    private static GroupOverviewModel EmptyOverview() => new(
        Group: new GroupModel("g1", "EUR", false),
        Summary: new GroupSummaryModel("g1", 0, 0, 0, 0),
        Participants: Array.Empty<ParticipantModel>(),
        EconomicUnits: Array.Empty<EconomicUnitModel>(),
        Expenses: Array.Empty<ExpenseModel>(),
        Transfers: Array.Empty<TransferModel>(),
        BalancesByParticipant: Array.Empty<BalanceModel>(),
        BalancesByEconomicUnitOwner: Array.Empty<BalanceModel>(),
        SettlementByParticipant: new SettlementPlanModel(SettlementMode.Participant, Array.Empty<SettlementTransferModel>()),
        SettlementByEconomicUnitOwner: new SettlementPlanModel(SettlementMode.EconomicUnitOwner, Array.Empty<SettlementTransferModel>()));

    private static IGroupPageDataService MockDataService(bool isShared, string? ownerId = "user-1")
    {
        var ds = Substitute.For<IGroupPageDataService>();
        var model = new GroupWorkspaceModel(
            GroupId: "g1",
            GroupName: "Trip",
            Overview: EmptyOverview(),
            ExpenseIcons: new Dictionary<string, string>(),
            LastOpenedUtc: null,
            ImagePath: null,
            IsShared: isShared,
            IsReadOnly: false,
            OwnerId: ownerId);
        ds.GetGroupWorkspaceAsync(Arg.Any<string>()).Returns(model);
        ds.GetGroupWorkspaceAsync().Returns(model);
        return ds;
    }

    private static IAuthPort MockAuth(string userId = "user-1")
    {
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(userId);
        return auth;
    }

    [Fact]
    public async Task SharedOwner_ShowInviteAction_True()
    {
        var vm = new GroupViewModel(MockDataService(true, "user-1"), null, MockAuth("user-1"));
        await vm.LoadAsync();

        Assert.True(vm.ShowInviteAction);
    }

    [Fact]
    public async Task SharedMember_ShowInviteAction_False()
    {
        var vm = new GroupViewModel(MockDataService(true, "owner-1"), null, MockAuth("member-1"));
        await vm.LoadAsync();

        Assert.False(vm.ShowInviteAction);
    }

    [Fact]
    public async Task LocalGroup_ShowInviteAction_False()
    {
        var vm = new GroupViewModel(MockDataService(false), null, MockAuth("user-1"));
        await vm.LoadAsync();

        Assert.False(vm.ShowInviteAction);
    }

    [Fact]
    public async Task SharedOwner_InviteCommand_RaisesEvent()
    {
        var vm = new GroupViewModel(MockDataService(true, "user-1"), null, MockAuth("user-1"));
        await vm.LoadAsync();

        string? receivedGroupId = null;
        vm.InviteRequested += (_, gid) => receivedGroupId = gid;
        vm.NavigateToInviteCommand.Execute(null);

        Assert.Equal("g1", receivedGroupId);
    }

    [Fact]
    public async Task SharedMember_InviteCommand_DoesNotRaise()
    {
        var vm = new GroupViewModel(MockDataService(true, "owner-1"), null, MockAuth("member-1"));
        await vm.LoadAsync();

        string? receivedGroupId = null;
        vm.InviteRequested += (_, gid) => receivedGroupId = gid;
        vm.NavigateToInviteCommand.Execute(null);

        Assert.Null(receivedGroupId);
    }

    [Fact]
    public async Task SharedGroup_ShowMembersAction_True()
    {
        var vm = new GroupViewModel(MockDataService(true, "user-1"), null, MockAuth("member-1"));
        await vm.LoadAsync();

        Assert.True(vm.ShowMembersAction);
    }

    [Fact]
    public async Task LocalGroup_ShowMembersAction_False()
    {
        var vm = new GroupViewModel(MockDataService(false), null, MockAuth("user-1"));
        await vm.LoadAsync();

        Assert.False(vm.ShowMembersAction);
    }
}
