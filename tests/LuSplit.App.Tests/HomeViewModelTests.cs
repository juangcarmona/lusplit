using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class HomeViewModelTests
{
    // ---- helpers ----

    private static GroupWorkspaceModel EmptyWorkspace(string groupId = "g1") =>
        new(groupId,
            "Test Group",
            new GroupOverviewModel(
                new GroupModel(groupId, "USD", false),
                new GroupSummaryModel(groupId, 0, 0, 0, 0),
                [],
                [],
                [],
                [],
                [],
                [],
                new SettlementPlanModel(SettlementMode.Participant, []),
                new SettlementPlanModel(SettlementMode.EconomicUnitOwner, [])),
            new Dictionary<string, string>(),
            null);

    private static GroupWorkspaceModel WorkspaceWith(params ExpenseModel[] expenses)
    {
        var ws = EmptyWorkspace();
        return ws with { Overview = ws.Overview with { Expenses = expenses } };
    }

    private static ExpenseModel SimpleExpense(string id) =>
        new(id, "g1", $"Expense {id}", "p1", 1000, "2024-01-01",
            new SplitDefinition([]), null);

    private static IHomeDataService ServiceReturning(GroupWorkspaceModel workspace)
    {
        var svc = Substitute.For<IHomeDataService>();
        svc.GetGroupWorkspaceAsync().Returns(workspace);
        return svc;
    }

    private static IHomeDataService ServiceThrowingNoGroups()
    {
        var svc = Substitute.For<IHomeDataService>();
        svc.GetGroupWorkspaceAsync().ThrowsAsync(new NoGroupsAvailableException());
        return svc;
    }

    // ---- LoadAsync: no group ----

    [Fact]
    public async Task LoadAsync_NoGroups_SetsHasGroupFalse()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());

        await vm.LoadAsync();

        Assert.False(vm.HasGroup);
    }

    [Fact]
    public async Task LoadAsync_NoGroups_ShowNoGroupsEmptyStateTrue()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());

        await vm.LoadAsync();

        Assert.True(vm.ShowNoGroupsEmptyState);
    }

    [Fact]
    public async Task LoadAsync_NoGroups_ClearsAllCollections()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());

        await vm.LoadAsync();

        Assert.Empty(vm.Events);
        Assert.Empty(vm.RecentEvents);
        Assert.Empty(vm.Balances);
        Assert.Empty(vm.WhoOwesWho);
    }

    [Fact]
    public async Task LoadAsync_NoGroups_ClearsGroupName()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());

        await vm.LoadAsync();

        Assert.Equal(string.Empty, vm.GroupName);
    }

    // ---- LoadAsync: with group ----

    [Fact]
    public async Task LoadAsync_WithGroup_SetsHasGroupTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.True(vm.HasGroup);
    }

    [Fact]
    public async Task LoadAsync_WithGroup_SetsGroupName()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.Equal("Test Group", vm.GroupName);
    }

    [Fact]
    public async Task LoadAsync_WithExpenses_PopulatesEvents()
    {
        var vm = new HomeViewModel(ServiceReturning(WorkspaceWith(SimpleExpense("e1"), SimpleExpense("e2"))));

        await vm.LoadAsync();

        Assert.Equal(2, vm.Events.Count);
    }

    [Fact]
    public async Task LoadAsync_WithExpenses_PopulatesRecentEvents()
    {
        var vm = new HomeViewModel(ServiceReturning(WorkspaceWith(SimpleExpense("e1"), SimpleExpense("e2"))));

        await vm.LoadAsync();

        Assert.Equal(2, vm.RecentEvents.Count);
    }

    [Fact]
    public async Task LoadAsync_ManyExpenses_RecentEventsLimitedToFive()
    {
        var expenses = Enumerable.Range(1, 7).Select(i => SimpleExpense($"e{i}")).ToArray();
        var vm = new HomeViewModel(ServiceReturning(WorkspaceWith(expenses)));

        await vm.LoadAsync();

        Assert.Equal(7, vm.Events.Count);
        Assert.Equal(5, vm.RecentEvents.Count);
    }

    [Fact]
    public async Task LoadAsync_WithExpenses_HasEventsIsTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(WorkspaceWith(SimpleExpense("e1"))));

        await vm.LoadAsync();

        Assert.True(vm.HasEvents);
    }

    [Fact]
    public async Task LoadAsync_EmptyGroup_HasEventsIsFalse()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        Assert.False(vm.HasEvents);
    }

    [Fact]
    public async Task LoadAsync_EmptyGroup_ShowOverviewEmptyStateTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        await vm.LoadAsync();

        // Default tab is Overview, no events → empty state
        Assert.True(vm.ShowOverviewEmptyState);
    }

    // ---- Tab commands ----

    [Fact]
    public void DefaultTab_ShowOverviewIsTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        Assert.True(vm.ShowOverview);
        Assert.False(vm.ShowExpenses);
        Assert.False(vm.ShowBalances);
    }

    [Fact]
    public void SelectExpensesTabCommand_SetsShowExpenses()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        vm.SelectExpensesTabCommand.Execute(null);

        Assert.False(vm.ShowOverview);
        Assert.True(vm.ShowExpenses);
        Assert.False(vm.ShowBalances);
    }

    [Fact]
    public void SelectBalancesTabCommand_SetsShowBalances()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.False(vm.ShowOverview);
        Assert.False(vm.ShowExpenses);
        Assert.True(vm.ShowBalances);
    }

    [Fact]
    public void SelectOverviewTabCommand_RestoresShowOverview()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        vm.SelectExpensesTabCommand.Execute(null);

        vm.SelectOverviewTabCommand.Execute(null);

        Assert.True(vm.ShowOverview);
    }

    [Fact]
    public void SelectSameTabTwice_DoesNotFireTabChanged()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        vm.SelectExpensesTabCommand.Execute(null); // set to Expenses

        var fired = 0;
        vm.TabChanged += (_, _) => fired++;

        vm.SelectExpensesTabCommand.Execute(null); // same tab again

        Assert.Equal(0, fired);
    }

    [Fact]
    public void SelectDifferentTab_FiresTabChanged()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));

        var fired = 0;
        vm.TabChanged += (_, _) => fired++;

        vm.SelectExpensesTabCommand.Execute(null);

        Assert.Equal(1, fired);
    }

    // ---- ShowAddExpenseButton / ShowSettleUpButton ----

    [Fact]
    public async Task ShowAddExpenseButton_HasGroup_OverviewTab_IsTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        Assert.True(vm.ShowAddExpenseButton);
    }

    [Fact]
    public async Task ShowAddExpenseButton_HasGroup_BalancesTab_IsFalse()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.False(vm.ShowAddExpenseButton);
    }

    [Fact]
    public async Task ShowSettleUpButton_BalancesTab_HasGroup_IsTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.True(vm.ShowSettleUpButton);
    }

    [Fact]
    public async Task ShowSettleUpButton_OverviewTab_IsFalse()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        Assert.False(vm.ShowSettleUpButton);
    }

    [Fact]
    public async Task ShowAddExpenseButton_NoGroup_IsFalse()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());
        await vm.LoadAsync();

        Assert.False(vm.ShowAddExpenseButton);
    }

    // ---- ShowBalancesEmptyState ----

    [Fact]
    public async Task ShowBalancesEmptyState_BalancesTab_NoData_IsTrue()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.True(vm.ShowBalancesEmptyState);
    }

    [Fact]
    public async Task ShowBalancesEmptyState_NotOnBalancesTab_IsFalse()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        await vm.LoadAsync();

        // Default is Overview tab
        Assert.False(vm.ShowBalancesEmptyState);
    }

    // ---- PropertyChanged notifications ----

    [Fact]
    public async Task LoadAsync_WithGroup_RaisesGroupNameChanged()
    {
        var vm = new HomeViewModel(ServiceReturning(EmptyWorkspace()));
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await vm.LoadAsync();

        Assert.Contains(nameof(vm.GroupName), raised);
    }

    [Fact]
    public async Task LoadAsync_NoGroup_RaisesHasGroupChanged()
    {
        var vm = new HomeViewModel(ServiceThrowingNoGroups());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await vm.LoadAsync();

        Assert.Contains(nameof(vm.HasGroup), raised);
    }
}
