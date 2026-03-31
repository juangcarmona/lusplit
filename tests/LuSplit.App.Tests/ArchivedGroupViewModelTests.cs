using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Expenses;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class ArchivedGroupViewModelTests
{
    // ── helpers ───────────────────────────────────────────────────────────

    private static GroupWorkspaceModel EmptyWorkspace(
        string groupId = "g1",
        string groupName = "Bali 2025",
        string? imagePath = null) =>
        new(groupId,
            groupName,
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
            null,
            imagePath);

    private static IArchivedGroupDataService ServiceReturning(GroupWorkspaceModel workspace)
    {
        var svc = Substitute.For<IArchivedGroupDataService>();
        svc.GetGroupWorkspaceAsync(Arg.Any<string>()).Returns(workspace);
        return svc;
    }

    private static ArchivedGroupViewModel LoadedVm(GroupWorkspaceModel workspace)
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(workspace));
        vm.PrepareForGroup(workspace.GroupId);
        return vm;
    }

    // ── LoadAsync: basic state ────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsGroupName()
    {
        var vm = LoadedVm(EmptyWorkspace(groupName: "Bali 2025"));

        await vm.LoadAsync();

        Assert.Equal("Bali 2025", vm.GroupName);
    }

    [Fact]
    public async Task LoadAsync_SetsGroupImagePath()
    {
        var vm = LoadedVm(EmptyWorkspace(imagePath: "/path/img.jpg"));

        await vm.LoadAsync();

        Assert.Equal("/path/img.jpg", vm.GroupImagePath);
    }

    [Fact]
    public async Task LoadAsync_NullImage_GroupImagePathNull()
    {
        var vm = LoadedVm(EmptyWorkspace(imagePath: null));

        await vm.LoadAsync();

        Assert.Null(vm.GroupImagePath);
    }

    [Fact]
    public async Task LoadAsync_SetsGroupMetaText()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        // GroupPresentationMapper.FormatCompactPeopleAndEvents returns a non-null string
        Assert.NotNull(vm.GroupMetaText);
    }

    [Fact]
    public async Task LoadAsync_UsesCorrectGroupId()
    {
        var svc = Substitute.For<IArchivedGroupDataService>();
        svc.GetGroupWorkspaceAsync("g42").Returns(EmptyWorkspace("g42", "Trip 42"));
        var vm = new ArchivedGroupViewModel(svc);
        vm.PrepareForGroup("g42");

        await vm.LoadAsync();

        Assert.Equal("Trip 42", vm.GroupName);
        await svc.Received(1).GetGroupWorkspaceAsync("g42");
    }

    // ── LoadAsync: derived image flags ────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WithImagePath_HasGroupImageTrue()
    {
        var vm = LoadedVm(EmptyWorkspace(imagePath: "/img.jpg"));

        await vm.LoadAsync();

        Assert.True(vm.HasGroupImage);
        Assert.False(vm.HasNoGroupImage);
    }

    [Fact]
    public async Task LoadAsync_NullImagePath_HasGroupImageFalse()
    {
        var vm = LoadedVm(EmptyWorkspace(imagePath: null));

        await vm.LoadAsync();

        Assert.False(vm.HasGroupImage);
        Assert.True(vm.HasNoGroupImage);
    }

    [Fact]
    public async Task LoadAsync_EmptyImagePath_HasGroupImageFalse()
    {
        var vm = LoadedVm(EmptyWorkspace(imagePath: string.Empty));

        await vm.LoadAsync();

        Assert.False(vm.HasGroupImage);
    }

    // ── LoadAsync: empty-state flags with no events ───────────────────────

    [Fact]
    public async Task LoadAsync_NoEvents_HasEventsFalse()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.False(vm.HasEvents);
    }

    [Fact]
    public async Task LoadAsync_NoEvents_ShowOverviewEmptyStateTrue()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.True(vm.ShowOverviewEmptyState);
    }

    [Fact]
    public async Task LoadAsync_NoEvents_ShowExpensesEmptyStateFalse_WhenNotOnExpensesTab()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.False(vm.ShowExpensesEmptyState); // default tab is Overview
    }

    [Fact]
    public async Task LoadAsync_NoEvents_ShowBalancesEmptyStateFalse_WhenNotOnBalancesTab()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.False(vm.ShowBalancesEmptyState); // default tab is Overview
    }

    // ── Default tab state ─────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsOverviewTab()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));

        Assert.True(vm.IsOverviewTab);
        Assert.False(vm.IsExpensesTab);
        Assert.False(vm.IsBalancesTab);
    }

    [Fact]
    public void InitialState_ShowOverviewTrue()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));

        Assert.True(vm.ShowOverview);
        Assert.False(vm.ShowExpenses);
        Assert.False(vm.ShowBalances);
    }

    // ── Tab switching ─────────────────────────────────────────────────────

    [Fact]
    public void SelectExpensesTabCommand_SwitchesToExpensesTab()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));

        vm.SelectExpensesTabCommand.Execute(null);

        Assert.True(vm.IsExpensesTab);
        Assert.True(vm.ShowExpenses);
        Assert.False(vm.ShowOverview);
        Assert.False(vm.ShowBalances);
    }

    [Fact]
    public void SelectBalancesTabCommand_SwitchesToBalancesTab()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.True(vm.IsBalancesTab);
        Assert.True(vm.ShowBalances);
        Assert.False(vm.ShowOverview);
        Assert.False(vm.ShowExpenses);
    }

    [Fact]
    public void SelectOverviewTabCommand_FromExpenses_SwitchesBack()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));
        vm.SelectExpensesTabCommand.Execute(null);

        vm.SelectOverviewTabCommand.Execute(null);

        Assert.True(vm.IsOverviewTab);
        Assert.True(vm.ShowOverview);
    }

    [Fact]
    public void SelectExpensesTabCommand_WhenAlreadyActive_NoChange()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));
        vm.SelectExpensesTabCommand.Execute(null);

        // Execute again — should remain on expenses
        vm.SelectExpensesTabCommand.Execute(null);

        Assert.True(vm.IsExpensesTab);
    }

    // ── Tab switching: empty-state visibility after tab change ────────────

    [Fact]
    public async Task SelectExpensesTab_NoEvents_ShowExpensesEmptyStateTrue()
    {
        var vm = LoadedVm(EmptyWorkspace());
        await vm.LoadAsync();

        vm.SelectExpensesTabCommand.Execute(null);

        Assert.True(vm.ShowExpensesEmptyState);
        Assert.False(vm.ShowOverviewEmptyState);
    }

    [Fact]
    public async Task SelectBalancesTab_NoEvents_ShowBalancesEmptyStateTrue()
    {
        var vm = LoadedVm(EmptyWorkspace());
        await vm.LoadAsync();

        vm.SelectBalancesTabCommand.Execute(null);

        Assert.True(vm.ShowBalancesEmptyState);
    }

    [Fact]
    public async Task SelectOverviewTab_NoEvents_ShowOverviewEmptyStateRestored()
    {
        var vm = LoadedVm(EmptyWorkspace());
        await vm.LoadAsync();
        vm.SelectExpensesTabCommand.Execute(null);

        vm.SelectOverviewTabCommand.Execute(null);

        Assert.True(vm.ShowOverviewEmptyState);
        Assert.False(vm.ShowExpensesEmptyState);
    }

    // ── RequestExportCommand ──────────────────────────────────────────────

    [Fact]
    public void RequestExportCommand_BeforeLoad_DoesNotFire()
    {
        var vm = new ArchivedGroupViewModel(ServiceReturning(EmptyWorkspace()));
        // groupId is empty string — should not fire
        var fired = false;
        vm.ExportRequested += (_, _) => fired = true;

        vm.RequestExportCommand.Execute(null);

        Assert.False(fired);
    }

    [Fact]
    public async Task RequestExportCommand_AfterLoad_FiresWithGroupId()
    {
        var vm = LoadedVm(EmptyWorkspace("g55"));
        await vm.LoadAsync();
        string? receivedId = null;
        vm.ExportRequested += (_, id) => receivedId = id;

        vm.RequestExportCommand.Execute(null);

        Assert.Equal("g55", receivedId);
    }

    // ── Collection building: empty workspace ──────────────────────────────

    [Fact]
    public async Task LoadAsync_EmptyWorkspace_CollectionsAreEmpty()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.Empty(vm.Events);
        Assert.Empty(vm.RecentEvents);
        Assert.Empty(vm.Balances);
        Assert.Empty(vm.WhoOwesWho);
    }

    [Fact]
    public async Task LoadAsync_EmptyWorkspace_ShowWhoOwesWhatSectionFalse()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.False(vm.ShowWhoOwesWhatSection);
    }

    [Fact]
    public async Task LoadAsync_EmptyWorkspace_ShowBalancesSectionFalse()
    {
        var vm = LoadedVm(EmptyWorkspace());

        await vm.LoadAsync();

        Assert.False(vm.ShowBalancesSection);
    }

    // ── PrepareForGroup ───────────────────────────────────────────────────

    [Fact]
    public async Task PrepareForGroup_SetsGroupIdUsedForLoad()
    {
        var svc = Substitute.For<IArchivedGroupDataService>();
        svc.GetGroupWorkspaceAsync("abc").Returns(EmptyWorkspace("abc", "Archive Trip"));
        var vm = new ArchivedGroupViewModel(svc);
        vm.PrepareForGroup("abc");

        await vm.LoadAsync();

        Assert.Equal("Archive Trip", vm.GroupName);
    }
}
