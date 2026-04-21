using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.Queries;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.Groups;

public sealed class GetGroupMembersQueryTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";
    private const string MemberId = "member-1";

    private readonly IGroupMembershipRepository _repo = Substitute.For<IGroupMembershipRepository>();

    private GetGroupMembersQuery CreateSut() => new(_repo);

    private void SetupMembers(params GroupMembership[] memberships)
    {
        _repo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(memberships);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAllNonRevokedMembers()
    {
        SetupMembers(
            new GroupMembership(GroupId, OwnerId, MemberRole.Owner, DateTimeOffset.UtcNow, false, null),
            new GroupMembership(GroupId, MemberId, MemberRole.Member, DateTimeOffset.UtcNow, false, null));

        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ExecuteAsync_OwnerIsFlagged()
    {
        SetupMembers(
            new GroupMembership(GroupId, OwnerId, MemberRole.Owner, DateTimeOffset.UtcNow, false, null),
            new GroupMembership(GroupId, MemberId, MemberRole.Member, DateTimeOffset.UtcNow, false, null));

        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId);

        var owner = result.Single(m => m.UserId == OwnerId);
        var member = result.Single(m => m.UserId == MemberId);
        Assert.True(owner.IsOwner);
        Assert.False(member.IsOwner);
    }

    [Fact]
    public async Task ExecuteAsync_DisplayNameFromDictionary_IsUsed()
    {
        SetupMembers(
            new GroupMembership(GroupId, OwnerId, MemberRole.Owner, DateTimeOffset.UtcNow, false, null));

        var displayNames = new Dictionary<string, string> { [OwnerId] = "Alice" };
        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId, displayNames);

        Assert.Equal("Alice", result[0].DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_NoDisplayName_FallsBackToUserId()
    {
        SetupMembers(
            new GroupMembership(GroupId, MemberId, MemberRole.Member, DateTimeOffset.UtcNow, false, null));

        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId);

        Assert.Equal(MemberId, result[0].DisplayName);
    }
}
