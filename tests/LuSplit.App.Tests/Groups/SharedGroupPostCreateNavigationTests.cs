using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
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
    {
        var ds = dataService ?? MockDataService();
        var groupRepo = Substitute.For<IGroupRepository>();
        groupRepo.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Group("g-123", "EUR", false));
        var regPort = Substitute.For<IGroupRegistrationPort>();
        regPort.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateGroupResponse("g-123", "c-1"));
        regPort.GetGroupInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GroupInfoResponse("g-123", "owner-1", 1, DateTimeOffset.UtcNow));
        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        var memberRepo = Substitute.For<IGroupMembershipRepository>();
        memberRepo.GetByGroupIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GroupMembership>());
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns("owner-1");
        var convert = new ConvertGroupToSharedUseCase(groupRepo, regPort, stateRepo, memberRepo, keyStorage, auth);
        var refresh = new RefreshSharedGroupContextUseCase(regPort, stateRepo, memberRepo);
        return new CreateGroupViewModel(ds, convert, refresh, deviceIdProvider: () => "Phone");
    }

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
