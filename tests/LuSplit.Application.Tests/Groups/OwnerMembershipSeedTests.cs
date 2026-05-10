using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.Groups;

public sealed class OwnerMembershipSeedTests
{
    [Fact]
    public async Task CreateSharedGroup_SeedsOwnerMembership()
    {
        var repos = new InMemoryQueryRepositories();
        var registration = Substitute.For<IGroupRegistrationPort>();
        var sharedState = new InMemorySharedGroupStateRepository();
        var membershipRepo = Substitute.For<IGroupMembershipRepository>();
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns("user-1");

        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var req = call.Arg<CreateGroupRequest>();
                return Task.FromResult(new CreateGroupResponse(req.GroupId, $"grp-{req.GroupId}"));
            });

        var sut = new CreateSharedGroupUseCase(
            repos, registration, sharedState, membershipRepo, keyStorage, auth,
            new SequentialIdGenerator());

        await sut.ExecuteAsync("EUR", "device-1", CancellationToken.None);

        await membershipRepo.Received(1).UpsertAsync(
            Arg.Is<GroupMembership>(m =>
                m.UserId == "user-1" &&
                m.Role == MemberRole.Owner &&
                !m.IsRevoked),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConvertGroupToShared_SeedsOwnerMembership()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "EUR", false));

        var registration = Substitute.For<IGroupRegistrationPort>();
        var sharedState = new InMemorySharedGroupStateRepository();
        var membershipRepo = Substitute.For<IGroupMembershipRepository>();
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns("user-1");

        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var req = call.Arg<CreateGroupRequest>();
                return Task.FromResult(new CreateGroupResponse(req.GroupId, $"grp-{req.GroupId}"));
            });

        var sut = new ConvertGroupToSharedUseCase(
            repos, registration, sharedState, membershipRepo, keyStorage, auth);

        await sut.ExecuteAsync("g1", "device-1", CancellationToken.None);

        await membershipRepo.Received(1).UpsertAsync(
            Arg.Is<GroupMembership>(m =>
                m.GroupId == "g1" &&
                m.UserId == "user-1" &&
                m.Role == MemberRole.Owner &&
                !m.IsRevoked),
            Arg.Any<CancellationToken>());
    }
}
