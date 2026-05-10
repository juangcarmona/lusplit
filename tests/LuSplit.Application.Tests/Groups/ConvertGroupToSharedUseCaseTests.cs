using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.UseCases;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.Application.Tests.Groups;

public sealed class ConvertGroupToSharedUseCaseTests
{
    private static (
        InMemoryQueryRepositories repos,
        IGroupRegistrationPort registration,
        InMemorySharedGroupStateRepository sharedState,
        ISecureKeyStoragePort keyStorage,
        IAuthPort auth,
        ConvertGroupToSharedUseCase sut
    ) BuildSut(string? userId = "user-1")
    {
        var repos = new InMemoryQueryRepositories();
        var registration = Substitute.For<IGroupRegistrationPort>();
        var sharedState = new InMemorySharedGroupStateRepository();
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(userId);

        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var req = call.Arg<CreateGroupRequest>();
                return Task.FromResult(new CreateGroupResponse(req.GroupId, $"grp-{req.GroupId}"));
            });

        var sut = new ConvertGroupToSharedUseCase(
            repos,
            registration,
            sharedState,
            Substitute.For<IGroupMembershipRepository>(),
            keyStorage,
            auth);

        return (repos, registration, sharedState, keyStorage, auth, sut);
    }

    [Fact]
    public async Task HappyPath_RegistersGroupAndPersistsSharedState()
    {
        var (repos, registration, sharedState, keyStorage, _, sut) = BuildSut();
        repos.Groups.Add(new Group("g1", "EUR", false));

        await sut.ExecuteAsync("g1", "device-1", CancellationToken.None);

        var state = await sharedState.GetByGroupIdAsync("g1", CancellationToken.None);
        Assert.NotNull(state);
        Assert.True(state!.IsShared);
        Assert.Equal("user-1", state.OwnerId);
        Assert.Equal(SyncStatus.PendingLocalChanges, state.SyncStatus);

        await registration.Received(1).RegisterGroupAsync(
            Arg.Is<CreateGroupRequest>(r =>
                r.GroupId == "g1" &&
                r.OwnerId == "user-1" &&
                r.OwnerDeviceId == "device-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_StoresWrappedKeyAndPrivateKey()
    {
        var (repos, _, _, keyStorage, _, sut) = BuildSut();
        repos.Groups.Add(new Group("g1", "EUR", false));

        await sut.ExecuteAsync("g1", "device-1", CancellationToken.None);

        await keyStorage.Received(1).StoreWrappedKeyAsync(
            "g1", 1, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await keyStorage.Received(1).StorePrivateKeyAsync(
            "device-1", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_ThrowsValidationError()
    {
        var (repos, _, _, _, _, sut) = BuildSut(userId: null);
        repos.Groups.Add(new Group("g1", "EUR", false));

        await Assert.ThrowsAsync<ValidationError>(() =>
            sut.ExecuteAsync("g1", "device-1", CancellationToken.None));
    }

    [Fact]
    public async Task GroupNotFound_ThrowsNotFoundError()
    {
        var (_, _, _, _, _, sut) = BuildSut();

        await Assert.ThrowsAsync<NotFoundError>(() =>
            sut.ExecuteAsync("missing", "device-1", CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyShared_ThrowsValidationError()
    {
        var (repos, _, sharedState, _, _, sut) = BuildSut();
        repos.Groups.Add(new Group("g1", "EUR", false));
        await sharedState.SaveAsync("g1", new SharedGroupState(
            IsShared: true,
            RemoteContainerName: "grp-g1",
            OwnerId: "user-1",
            CurrentKeyVersion: 1,
            SyncStatus: SyncStatus.UpToDate,
            IsReadOnly: false), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationError>(() =>
            sut.ExecuteAsync("g1", "device-1", CancellationToken.None));
    }

    [Fact]
    public async Task ConflictRetry_SameOwner_RecoversByFetchingGroupInfo()
    {
        var (repos, registration, sharedState, keyStorage, _, sut) = BuildSut();
        repos.Groups.Add(new Group("g1", "EUR", false));

        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Group g1 is already registered."));

        registration.GetGroupInfoAsync("g1", Arg.Any<CancellationToken>())
            .Returns(new GroupInfoResponse("g1", "user-1", 1, DateTimeOffset.UtcNow));

        await sut.ExecuteAsync("g1", "device-1", CancellationToken.None);

        var state = await sharedState.GetByGroupIdAsync("g1", CancellationToken.None);
        Assert.NotNull(state);
        Assert.True(state!.IsShared);

        await keyStorage.Received(1).StoreWrappedKeyAsync(
            "g1", 1, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await keyStorage.Received(1).StorePrivateKeyAsync(
            "device-1", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConflictRetry_DifferentOwner_Throws()
    {
        var (repos, registration, _, _, _, sut) = BuildSut();
        repos.Groups.Add(new Group("g1", "EUR", false));

        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Group g1 is already registered."));

        registration.GetGroupInfoAsync("g1", Arg.Any<CancellationToken>())
            .Returns(new GroupInfoResponse("g1", "other-user", 1, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync("g1", "device-1", CancellationToken.None));
    }
}
