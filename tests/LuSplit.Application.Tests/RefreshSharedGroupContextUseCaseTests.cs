using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.UseCases;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests;

public sealed class RefreshSharedGroupContextUseCaseTests
{
    private const string GroupId = "grp-1";
    private const string OwnerId = "owner-1";

    private static (RefreshSharedGroupContextUseCase useCase,
        IGroupRegistrationPort registrationPort,
        ISharedGroupStateRepository stateRepo,
        IGroupMembershipRepository membershipRepo) Build(
            SharedGroupState? existingState = null,
            GroupMembership[]? existingMembers = null)
    {
        var registrationPort = Substitute.For<IGroupRegistrationPort>();
        registrationPort.GetGroupInfoAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new GroupInfoResponse(GroupId, OwnerId, 1, DateTimeOffset.UtcNow));

        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        stateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(existingState);

        var membershipRepo = Substitute.For<IGroupMembershipRepository>();
        membershipRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(existingMembers ?? Array.Empty<GroupMembership>());

        var useCase = new RefreshSharedGroupContextUseCase(registrationPort, stateRepo, membershipRepo);

        return (useCase, registrationPort, stateRepo, membershipRepo);
    }

    [Fact]
    public async Task ExecuteAsync_SavesSharedGroupState()
    {
        var (useCase, _, stateRepo, _) = Build();

        await useCase.ExecuteAsync(GroupId);

        await stateRepo.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.IsShared && s.OwnerId == OwnerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTrue()
    {
        var (useCase, _, _, _) = Build();

        var result = await useCase.ExecuteAsync(GroupId);

        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_NoExistingState_GeneratesContainerName()
    {
        var (useCase, _, stateRepo, _) = Build(existingState: null);

        await useCase.ExecuteAsync(GroupId);

        await stateRepo.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.RemoteContainerName.StartsWith("grp-")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExistingState_PreservesContainerName()
    {
        var existing = new SharedGroupState(true, "my-container", OwnerId, 1, SyncStatus.UpToDate, false);
        var (useCase, _, stateRepo, _) = Build(existingState: existing);

        await useCase.ExecuteAsync(GroupId);

        await stateRepo.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.RemoteContainerName == "my-container"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoOwnerMembership_CreatesOne()
    {
        var (useCase, _, _, membershipRepo) = Build();

        await useCase.ExecuteAsync(GroupId);

        await membershipRepo.Received(1).UpsertAsync(
            Arg.Is<GroupMembership>(m =>
                m.GroupId == GroupId
                && m.UserId == OwnerId
                && m.Role == MemberRole.Owner),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OwnerMembershipExists_DoesNotDuplicate()
    {
        var ownerMembership = new GroupMembership(GroupId, OwnerId, MemberRole.Owner, DateTimeOffset.UtcNow, false, null);
        var (useCase, _, _, membershipRepo) = Build(existingMembers: new[] { ownerMembership });

        await useCase.ExecuteAsync(GroupId);

        await membershipRepo.DidNotReceive().UpsertAsync(
            Arg.Any<GroupMembership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesKeyVersionFromControlPlane()
    {
        var (useCase, registrationPort, stateRepo, _) = Build();
        registrationPort.GetGroupInfoAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(new GroupInfoResponse(GroupId, OwnerId, 5, DateTimeOffset.UtcNow));

        await useCase.ExecuteAsync(GroupId);

        await stateRepo.Received(1).SaveAsync(
            GroupId,
            Arg.Is<SharedGroupState>(s => s.CurrentKeyVersion == 5),
            Arg.Any<CancellationToken>());
    }
}
