using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups;
using NSubstitute;

namespace LuSplit.App.Tests.Groups;

public sealed class SharedGroupPostCreateNavigationTests
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
    public async Task SharedMode_Create_RaisesSharedGroupCreated_NotGroupCreated()
    {
        var vm = BuildVm();
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.SelectModeCommand.Execute("Shared");
        vm.ContinueCommand.Execute(null);
        vm.AddParticipant("Alice");

        var localFired = false;
        string? sharedId = null;
        vm.GroupCreated += (_, _) => localFired = true;
        vm.SharedGroupCreated += (_, id) => sharedId = id;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.False(localFired);
        Assert.Equal("g-123", sharedId);
    }

    [Fact]
    public async Task LocalMode_Create_RaisesGroupCreated_NotSharedGroupCreated()
    {
        var vm = BuildVm();
        vm.GroupName = "Local Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.ContinueCommand.Execute(null);
        vm.AddParticipant("Bob");

        var localFired = false;
        string? sharedId = null;
        vm.GroupCreated += (_, _) => localFired = true;
        vm.SharedGroupCreated += (_, id) => sharedId = id;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.True(localFired);
        Assert.Null(sharedId);
    }
}
