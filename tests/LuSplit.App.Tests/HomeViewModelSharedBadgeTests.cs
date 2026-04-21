using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Payments.Models;
using NSubstitute;

namespace LuSplit.App.Tests;

public sealed class HomeViewModelSharedBadgeTests
{
    private static GroupWorkspaceModel MakeWorkspace(bool isShared)
    {
        const string groupId = "g1";
        var overview = new GroupOverviewModel(
            new GroupModel(groupId, "USD", false),
            new GroupSummaryModel(groupId, 0, 0, 0, 0),
            [], [], [], [], [], [],
            new SettlementPlanModel(SettlementMode.Participant, []),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));

        return new GroupWorkspaceModel(
            groupId, "Trip", overview,
            new Dictionary<string, string>(),
            null,
            ImagePath: null,
            IsShared: isShared);
    }

    private static IHomeDataService ServiceReturning(GroupWorkspaceModel workspace)
    {
        var svc = Substitute.For<IHomeDataService>();
        svc.GetGroupWorkspaceAsync().Returns(workspace);
        return svc;
    }

    [Fact]
    public async Task LoadAsync_SharedGroup_SetsIsCurrentGroupSharedTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(MakeWorkspace(isShared: true)));

        await vm.LoadAsync();

        Assert.True(vm.IsCurrentGroupShared);
    }

    [Fact]
    public async Task LoadAsync_LocalGroup_IsCurrentGroupSharedFalse()
    {
        var vm = new HomeViewModel(ServiceReturning(MakeWorkspace(isShared: false)));

        await vm.LoadAsync();

        Assert.False(vm.IsCurrentGroupShared);
    }

    [Fact]
    public void InitialState_IsCurrentGroupSharedFalse()
    {
        var svc = Substitute.For<IHomeDataService>();
        svc.GetGroupWorkspaceAsync().Returns(MakeWorkspace(false));
        var vm = new HomeViewModel(svc);

        Assert.False(vm.IsCurrentGroupShared);
    }
}
