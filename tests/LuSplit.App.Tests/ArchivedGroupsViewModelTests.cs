using System.Collections.ObjectModel;
using LuSplit.App.Pages;
using LuSplit.App.Services;
using NSubstitute;

namespace LuSplit.App.Tests;

public class ArchivedGroupsViewModelTests
{
    private static GroupListItemModel MakeGroup(string id, string name = "Group A") =>
        new(id, name, "USD", false, "1 expense", "$0.00", string.Empty, DateTimeOffset.UtcNow);

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesGroups_FromDataService()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        dataService.GetArchivedGroupsAsync().Returns([MakeGroup("g1"), MakeGroup("g2")]);

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.LoadAsync();

        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("g1", vm.Groups[0].GroupId);
        Assert.Equal("g2", vm.Groups[1].GroupId);
    }

    [Fact]
    public async Task LoadAsync_ClearsOldGroups_BeforeRepopulating()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        dataService.GetArchivedGroupsAsync().Returns([MakeGroup("g1")]);

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.LoadAsync();

        dataService.GetArchivedGroupsAsync().Returns([MakeGroup("g2")]);
        await vm.LoadAsync();

        Assert.Single(vm.Groups);
        Assert.Equal("g2", vm.Groups[0].GroupId);
    }

    [Fact]
    public async Task LoadAsync_WhenNoGroups_LeavesGroupsEmpty()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        dataService.GetArchivedGroupsAsync().Returns(Array.Empty<GroupListItemModel>());

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.LoadAsync();

        Assert.Empty(vm.Groups);
    }

    // ── HandleDataChangedAsync ────────────────────────────────────────────────

    [Fact]
    public async Task HandleDataChangedAsync_ReloadsGroups()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        dataService.GetArchivedGroupsAsync().Returns([MakeGroup("g1")]);

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.HandleDataChangedAsync();

        Assert.Single(vm.Groups);
        Assert.Equal("g1", vm.Groups[0].GroupId);
    }

    [Fact]
    public async Task HandleDataChangedAsync_CallsDataService_EachTime()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        dataService.GetArchivedGroupsAsync().Returns([]);

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.HandleDataChangedAsync();
        await vm.HandleDataChangedAsync();

        await dataService.Received(2).GetArchivedGroupsAsync();
    }

    // ── ViewGroupCommand ──────────────────────────────────────────────────────

    [Fact]
    public void ViewGroupCommand_RaisesViewGroupRequested_WithGroupId()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        var vm = new ArchivedGroupsViewModel(dataService);

        string? receivedId = null;
        vm.ViewGroupRequested += (_, id) => receivedId = id;

        vm.ViewGroupCommand.Execute("group-123");

        Assert.Equal("group-123", receivedId);
    }

    [Fact]
    public void ViewGroupCommand_DoesNotRaiseEvent_WhenGroupIdIsEmpty()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        var vm = new ArchivedGroupsViewModel(dataService);

        var raised = false;
        vm.ViewGroupRequested += (_, _) => raised = true;

        vm.ViewGroupCommand.Execute(string.Empty);

        Assert.False(raised);
    }

    [Fact]
    public void ViewGroupCommand_DoesNotRaiseEvent_WhenGroupIdIsWhitespace()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        var vm = new ArchivedGroupsViewModel(dataService);

        var raised = false;
        vm.ViewGroupRequested += (_, _) => raised = true;

        vm.ViewGroupCommand.Execute("   ");

        Assert.False(raised);
    }

    // ── Groups observable collection ──────────────────────────────────────────

    [Fact]
    public async Task Groups_ReturnsItemsInLoadedOrder()
    {
        var dataService = Substitute.For<IArchivedGroupsDataService>();
        var items = new[]
        {
            MakeGroup("a", "Alpha"),
            MakeGroup("b", "Beta"),
            MakeGroup("c", "Gamma")
        };
        dataService.GetArchivedGroupsAsync().Returns(items);

        var vm = new ArchivedGroupsViewModel(dataService);
        await vm.LoadAsync();

        Assert.Equal(["Alpha", "Beta", "Gamma"], vm.Groups.Select(g => g.Name).ToArray());
    }
}
