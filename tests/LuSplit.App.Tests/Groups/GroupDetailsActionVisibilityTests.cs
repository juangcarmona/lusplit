using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Shared.Ports;
using NSubstitute;

namespace LuSplit.App.Tests.Groups;

public sealed class GroupDetailsActionVisibilityTests
{
    private static IGroupDetailsDataService MockDataService(
        bool isShared = false,
        bool isArchived = false,
        string? ownerId = "user-1")
    {
        var ds = Substitute.For<IGroupDetailsDataService>();
        ds.GetGroupDetailsAsync(Arg.Any<string>()).Returns(new GroupDetailsModel(
            GroupId: "g1",
            GroupName: "Trip",
            Currency: "EUR",
            IsArchived: isArchived,
            Members: [],
            ImagePath: null,
            IsShared: isShared,
            OwnerId: ownerId));
        return ds;
    }

    private static IAuthPort MockAuth(string userId = "user-1")
    {
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(userId);
        return auth;
    }

    private static GroupDetailsViewModel BuildVm(
        bool isShared = false,
        bool isArchived = false,
        string? ownerId = "user-1",
        string currentUserId = "user-1")
    {
        return new GroupDetailsViewModel(MockDataService(isShared, isArchived, ownerId), MockAuth(currentUserId));
    }

    [Fact]
    public async Task LocalGroup_CanConvertToShared()
    {
        var vm = BuildVm(isShared: false);
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.True(vm.CanConvertToShared);
        Assert.False(vm.CanInviteMembers);
        Assert.False(vm.CanManageMembers);
    }

    [Fact]
    public async Task SharedGroup_Owner_CanInviteAndManage()
    {
        var vm = BuildVm(isShared: true, ownerId: "user-1", currentUserId: "user-1");
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.True(vm.CanInviteMembers);
        Assert.True(vm.CanManageMembers);
        Assert.True(vm.CanManageSharing);
        Assert.False(vm.CanConvertToShared);
    }

    [Fact]
    public async Task SharedGroup_Member_CanManageButNotInvite()
    {
        var vm = BuildVm(isShared: true, ownerId: "owner-1", currentUserId: "member-1");
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.False(vm.CanInviteMembers);
        Assert.True(vm.CanManageMembers);
        Assert.False(vm.CanManageSharing);
    }

    [Fact]
    public async Task ArchivedGroup_CannotEdit()
    {
        var vm = BuildVm(isShared: false, isArchived: true);
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.False(vm.CanEditGroupSettings);
        Assert.False(vm.CanConvertToShared);
    }

    [Fact]
    public async Task SharedGroup_Member_CannotEditSettings()
    {
        var vm = BuildVm(isShared: true, ownerId: "owner-1", currentUserId: "member-1");
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.False(vm.CanEditGroupSettings);
    }

    [Fact]
    public async Task SharedGroup_Owner_CanEditSettings()
    {
        var vm = BuildVm(isShared: true, ownerId: "user-1", currentUserId: "user-1");
        vm.SetOverrideGroupId("g1");
        await vm.LoadAsync();

        Assert.True(vm.CanEditGroupSettings);
    }
}
