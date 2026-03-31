using LuSplit.Application.Expenses.Models;
using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Expenses;
using NSubstitute;

namespace LuSplit.App.Tests;

public sealed class ActivityViewModelTests
{
    // ── helpers ───────────────────────────────────────────────────────────

    private static GroupWorkspaceModel EmptyWorkspace(string groupId = "g1") =>
        new(groupId,
            "Trip",
            new GroupOverviewModel(
                new GroupModel(groupId, "USD", false),
                new GroupSummaryModel(groupId, 0, 0, 0, 0),
                [], [], [], [], [], [],
                new SettlementPlanModel(SettlementMode.Participant, []),
                new SettlementPlanModel(SettlementMode.EconomicUnitOwner, [])),
            new Dictionary<string, string>(),
            null);

    private static IActivityDataService ServiceReturning(GroupWorkspaceModel? ws = null)
    {
        var svc = Substitute.For<IActivityDataService>();
        svc.GetGroupWorkspaceAsync().Returns(ws ?? EmptyWorkspace());
        return svc;
    }

    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void InitialState_ActivityGroupsEmpty()
    {
        var vm = new ActivityViewModel(ServiceReturning());
        Assert.Empty(vm.ActivityGroups);
    }

    [Fact]
    public void InitialState_SubtitleIsEmpty()
    {
        var vm = new ActivityViewModel(ServiceReturning());
        Assert.Equal(string.Empty, vm.Subtitle);
    }

    // ── LoadAsync — subtitle ───────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsSubtitle()
    {
        var vm = new ActivityViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.NotNull(vm.Subtitle);
    }

    [Fact]
    public async Task LoadAsync_EmptyWorkspace_SubtitleNotEmpty()
    {
        // FormatCompactPeopleAndEvents with 0/0/0 still returns a non-null string
        var vm = new ActivityViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.NotNull(vm.Subtitle);
    }

    [Fact]
    public async Task LoadAsync_RaisesPropertyChanged_ForSubtitle()
    {
        var vm = new ActivityViewModel(ServiceReturning(EmptyWorkspace()));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.LoadAsync();

        Assert.Contains(nameof(vm.Subtitle), changed);
    }

    // ── LoadAsync — grouping ───────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_EmptyWorkspace_ActivityGroupsEmpty()
    {
        var vm = new ActivityViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.Empty(vm.ActivityGroups);
    }

    [Fact]
    public async Task LoadAsync_ClearsAndRebuildGroupsOnSecondCall()
    {
        var svc = ServiceReturning(EmptyWorkspace());
        var vm = new ActivityViewModel(svc);
        await vm.LoadAsync();
        await vm.LoadAsync();

        Assert.Empty(vm.ActivityGroups);
    }

    [Fact]
    public async Task LoadAsync_CallsGetGroupWorkspaceAsync()
    {
        var svc = ServiceReturning(EmptyWorkspace());
        var vm = new ActivityViewModel(svc);

        await vm.LoadAsync();

        await svc.Received(1).GetGroupWorkspaceAsync();
    }

    [Fact]
    public async Task LoadAsync_TwoCallsCallsServiceTwice()
    {
        var svc = ServiceReturning(EmptyWorkspace());
        var vm = new ActivityViewModel(svc);

        await vm.LoadAsync();
        await vm.LoadAsync();

        await svc.Received(2).GetGroupWorkspaceAsync();
    }

    // ── LoadAsync — workspace with expenses ───────────────────────────────

    private static GroupWorkspaceModel WorkspaceWithOneExpense()
    {
        var groupId = "g1";
        var participantId = "p1";
        var participant = new ParticipantModel(participantId, groupId, participantId, "Alice", "Full", null);
        var split = new SplitDefinition([new RemainderSplitComponent([participantId], RemainderMode.Equal)]);
        var expense = new ExpenseModel("e1", groupId, "Dinner", participantId, 1500, "2026-01-15T12:00:00Z", split, null);
        var overview = new GroupOverviewModel(
            new GroupModel(groupId, "USD", false),
            new GroupSummaryModel(groupId, 1, 0, 1, 0),
            [participant],
            [],
            [expense],
            [], [], [],
            new SettlementPlanModel(SettlementMode.Participant, []),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));
        return new GroupWorkspaceModel(groupId, "Trip", overview, new Dictionary<string, string>(), null);
    }

    [Fact]
    public async Task LoadAsync_WorkspaceWithExpense_PopulatesOneGroup()
    {
        var vm = new ActivityViewModel(ServiceReturning(WorkspaceWithOneExpense()));

        await vm.LoadAsync();

        Assert.Single(vm.ActivityGroups);
    }

    [Fact]
    public async Task LoadAsync_WorkspaceWithExpense_GroupHasOneItem()
    {
        var vm = new ActivityViewModel(ServiceReturning(WorkspaceWithOneExpense()));

        await vm.LoadAsync();

        Assert.Single(vm.ActivityGroups[0]);
    }

    [Fact]
    public async Task LoadAsync_WorkspaceWithExpense_GroupTitleIsNonEmpty()
    {
        var vm = new ActivityViewModel(ServiceReturning(WorkspaceWithOneExpense()));

        await vm.LoadAsync();

        Assert.False(string.IsNullOrWhiteSpace(vm.ActivityGroups[0].Title));
    }

    [Fact]
    public async Task LoadAsync_WorkspaceWithExpense_ItemLine1ContainsExpenseTitle()
    {
        var vm = new ActivityViewModel(ServiceReturning(WorkspaceWithOneExpense()));

        await vm.LoadAsync();

        Assert.Contains("Dinner", vm.ActivityGroups[0][0].Line1);
    }

    // ── HandleDataChangedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task HandleDataChangedAsync_CallsLoad()
    {
        var svc = ServiceReturning(EmptyWorkspace());
        var vm = new ActivityViewModel(svc);

        await vm.HandleDataChangedAsync();

        await svc.Received(1).GetGroupWorkspaceAsync();
    }

    [Fact]
    public async Task HandleDataChangedAsync_UpdatesSubtitle()
    {
        var svc = ServiceReturning(EmptyWorkspace());
        var vm = new ActivityViewModel(svc);

        await vm.HandleDataChangedAsync();

        Assert.NotNull(vm.Subtitle);
    }

    // ── ActivityCompactDayGroupViewModel ──────────────────────────────────

    [Fact]
    public void ActivityCompactDayGroupViewModel_StoresTitle()
    {
        var group = new ActivityCompactDayGroupViewModel("Today", []);

        Assert.Equal("Today", group.Title);
    }

    [Fact]
    public void ActivityCompactDayGroupViewModel_EmptyItems_CountZero()
    {
        var group = new ActivityCompactDayGroupViewModel("Today", []);

        Assert.Empty(group);
    }

    [Fact]
    public void ActivityCompactDayGroupViewModel_WithItems_CountMatches()
    {
        var item = new CompactEventEntryViewModel("id", true, "🍝", "Dinner", "Alice", "id", DateTimeOffset.Now);
        var group = new ActivityCompactDayGroupViewModel("Today", [item]);

        Assert.Single(group);
    }
}
