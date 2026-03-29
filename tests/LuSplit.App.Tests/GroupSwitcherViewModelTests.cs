using LuSplit.App.Pages;
using LuSplit.App.Services;
using NSubstitute;

namespace LuSplit.App.Tests;

public class GroupSwitcherViewModelTests
{
    private static IGroupSwitcherDataService MockDataService() => Substitute.For<IGroupSwitcherDataService>();

    private static GroupListItemModel MakeGroup(string id, string name, bool isCurrent = false)
        => new(id, name, "USD", isCurrent, "", "", "", DateTimeOffset.MinValue);

    private static GroupSwitcherViewModel BuildVm(IGroupSwitcherDataService? dataService = null)
        => new(dataService ?? MockDataService());

    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void InitialState_ActiveGroupsEmpty()
    {
        var vm = BuildVm();

        Assert.Empty(vm.ActiveGroups);
    }

    [Fact]
    public void InitialState_ArchivedGroupsEmpty()
    {
        var vm = BuildVm();

        Assert.Empty(vm.ArchivedGroups);
    }

    [Fact]
    public void InitialState_ShowArchivedIsFalse()
    {
        var vm = BuildVm();

        Assert.False(vm.ShowArchived);
    }

    // ── LoadAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesActiveGroups()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g1", "Alpha"), MakeGroup("g2", "Beta") });
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);

        await vm.LoadAsync();

        Assert.Equal(2, vm.ActiveGroups.Count);
    }

    [Fact]
    public async Task LoadAsync_PopulatesArchivedGroups()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        ds.GetArchivedGroupsAsync().Returns(new[] { MakeGroup("a1", "OldTrip") });
        var vm = BuildVm(ds);

        await vm.LoadAsync();

        Assert.Single(vm.ArchivedGroups);
    }

    [Fact]
    public async Task LoadAsync_MapsGroupIdCorrectly()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g1", "Alpha") });
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);

        await vm.LoadAsync();

        Assert.Equal("g1", vm.ActiveGroups[0].GroupId);
    }

    [Fact]
    public async Task LoadAsync_MapsNameCorrectly()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g1", "Alpha") });
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);

        await vm.LoadAsync();

        Assert.Equal("Alpha", vm.ActiveGroups[0].Name);
    }

    [Fact]
    public async Task LoadAsync_CurrentGroup_IsCurrentTrue()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g1", "Alpha", isCurrent: true) });
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);

        await vm.LoadAsync();

        Assert.True(vm.ActiveGroups[0].IsCurrent);
    }

    [Fact]
    public async Task LoadAsync_ClearsOldActiveGroupsOnReload()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g1", "Alpha") });
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);
        await vm.LoadAsync();

        ds.GetGroupsAsync().Returns(new[] { MakeGroup("g2", "Beta") });
        await vm.LoadAsync();

        Assert.Single(vm.ActiveGroups);
        Assert.Equal("g2", vm.ActiveGroups[0].GroupId);
    }

    [Fact]
    public async Task LoadAsync_ClearsOldArchivedGroupsOnReload()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        ds.GetArchivedGroupsAsync().Returns(new[] { MakeGroup("a1", "Old") });
        var vm = BuildVm(ds);
        await vm.LoadAsync();

        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        await vm.LoadAsync();

        Assert.Empty(vm.ArchivedGroups);
    }

    // ── ToggleArchivedCommand ──────────────────────────────────────────────

    [Fact]
    public void ToggleArchivedCommand_SetsShowArchivedTrue()
    {
        var vm = BuildVm();

        vm.ToggleArchivedCommand.Execute(null);

        Assert.True(vm.ShowArchived);
    }

    [Fact]
    public void ToggleArchivedCommand_CalledTwice_RestoresFalse()
    {
        var vm = BuildVm();
        vm.ToggleArchivedCommand.Execute(null);

        vm.ToggleArchivedCommand.Execute(null);

        Assert.False(vm.ShowArchived);
    }

    [Fact]
    public void ToggleArchivedCommand_RaisesPropertyChanged()
    {
        var vm = BuildVm();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ToggleArchivedCommand.Execute(null);

        Assert.Contains(nameof(vm.ShowArchived), changed);
    }

    // ── SelectGroupCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectGroupCommand_CallsSelectGroupAsync()
    {
        var ds = MockDataService();
        ds.GetGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        ds.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());
        var vm = BuildVm(ds);

        await vm.SelectGroupCommand.ExecuteAsync("g1");

        await ds.Received(1).SelectGroupAsync("g1");
    }

    [Fact]
    public async Task SelectGroupCommand_FiresGroupSelectedWithGroupId()
    {
        var ds = MockDataService();
        var vm = BuildVm(ds);
        string? receivedId = null;
        vm.GroupSelected += (_, id) => receivedId = id;

        await vm.SelectGroupCommand.ExecuteAsync("g1");

        Assert.Equal("g1", receivedId);
    }

    [Fact]
    public async Task SelectGroupCommand_DifferentGroupIds_PassesCorrectOne()
    {
        var ds = MockDataService();
        var vm = BuildVm(ds);
        string? receivedId = null;
        vm.GroupSelected += (_, id) => receivedId = id;

        await vm.SelectGroupCommand.ExecuteAsync("xyz");

        Assert.Equal("xyz", receivedId);
    }

    // ── NavigateToNewGroupCommand ──────────────────────────────────────────

    [Fact]
    public void NavigateToNewGroupCommand_FiresNewGroupRequested()
    {
        var vm = BuildVm();
        var fired = false;
        vm.NewGroupRequested += (_, _) => fired = true;

        vm.NavigateToNewGroupCommand.Execute(null);

        Assert.True(fired);
    }

    // ── GroupSwitcherItemViewModel (pure properties) ───────────────────────

    [Fact]
    public void ItemViewModel_CanSelect_FalseWhenCurrent()
    {
        var item = new GroupSwitcherItemViewModel("g", "Name", isCurrent: true);

        Assert.False(item.CanSelect);
    }

    [Fact]
    public void ItemViewModel_CanSelect_TrueWhenNotCurrent()
    {
        var item = new GroupSwitcherItemViewModel("g", "Name", isCurrent: false);

        Assert.True(item.CanSelect);
    }

    [Fact]
    public void ItemViewModel_DisplayName_CurrentGroup_AppendsSuffix()
    {
        var item = new GroupSwitcherItemViewModel("g", "Alpha", isCurrent: true);

        Assert.Contains("Alpha", item.DisplayName);
        Assert.Contains("GroupSwitcher_CurrentSuffix", item.DisplayName);
    }

    [Fact]
    public void ItemViewModel_DisplayName_NotCurrentGroup_IsName()
    {
        var item = new GroupSwitcherItemViewModel("g", "Alpha", isCurrent: false);

        Assert.Equal("Alpha", item.DisplayName);
    }

    [Fact]
    public void ItemViewModel_AvatarInitial_FirstLetterUppercase()
    {
        var item = new GroupSwitcherItemViewModel("g", "trip", isCurrent: false);

        Assert.Equal("T", item.AvatarInitial);
    }

    [Fact]
    public void ItemViewModel_AvatarInitial_EmptyName_ReturnsQuestionMark()
    {
        var item = new GroupSwitcherItemViewModel("g", "", isCurrent: false);

        Assert.Equal("?", item.AvatarInitial);
    }
}
