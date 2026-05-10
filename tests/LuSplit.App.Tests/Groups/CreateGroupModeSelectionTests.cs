using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups;
using NSubstitute;

namespace LuSplit.App.Tests.Groups;

public sealed class CreateGroupModeSelectionTests
{
    private static ICreateGroupDataService MockDataService(string returnGroupId = "g-123")
    {
        var ds = Substitute.For<ICreateGroupDataService>();
        ds.CreateGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GroupDraftMember>>())
            .Returns(returnGroupId);
        return ds;
    }

    private static CreateGroupViewModel BuildVm(ICreateGroupDataService? dataService = null)
        => new(dataService ?? MockDataService());

    [Fact]
    public void InitialMode_IsLocal()
    {
        var vm = BuildVm();
        Assert.Equal(GroupCollaborationMode.Local, vm.CollaborationMode);
        Assert.False(vm.IsSharedMode);
    }

    [Fact]
    public void SelectMode_Shared_SetsSharedMode()
    {
        var vm = BuildVm();
        vm.SelectModeCommand.Execute("Shared");

        Assert.Equal(GroupCollaborationMode.Shared, vm.CollaborationMode);
        Assert.True(vm.IsSharedMode);
    }

    [Fact]
    public void SelectMode_Local_KeepsLocalMode()
    {
        var vm = BuildVm();
        vm.SelectModeCommand.Execute("Shared");
        vm.SelectModeCommand.Execute("Local");

        Assert.Equal(GroupCollaborationMode.Local, vm.CollaborationMode);
        Assert.False(vm.IsSharedMode);
    }

    [Fact]
    public async Task CreateAsync_LocalMode_RaisesGroupCreated()
    {
        var ds = MockDataService();
        var vm = BuildVm(ds);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.ContinueCommand.Execute(null);
        vm.AddParticipant("Alice");

        var fired = false;
        vm.GroupCreated += (_, _) => fired = true;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.True(fired);
    }

    [Fact]
    public async Task CreateAsync_SharedMode_RaisesSharedGroupCreated()
    {
        var ds = MockDataService();
        var vm = BuildVm(ds);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.SelectModeCommand.Execute("Shared");
        vm.ContinueCommand.Execute(null);
        vm.AddParticipant("Alice");

        string? sharedGroupId = null;
        vm.SharedGroupCreated += (_, id) => sharedGroupId = id;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal("g-123", sharedGroupId);
    }

    [Fact]
    public async Task CreateAsync_SharedMode_DoesNotRaiseGroupCreated()
    {
        var ds = MockDataService();
        var vm = BuildVm(ds);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.SelectModeCommand.Execute("Shared");
        vm.ContinueCommand.Execute(null);
        vm.AddParticipant("Alice");

        var localFired = false;
        vm.GroupCreated += (_, _) => localFired = true;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.False(localFired);
    }

    [Fact]
    public void ModeHelperText_ChangesWithMode()
    {
        var vm = BuildVm();
        var localText = vm.ModeHelperText;

        vm.SelectModeCommand.Execute("Shared");
        var sharedText = vm.ModeHelperText;

        Assert.NotEqual(localText, sharedText);
        Assert.Contains("shared", sharedText, StringComparison.OrdinalIgnoreCase);
    }
}
