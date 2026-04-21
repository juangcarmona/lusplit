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

public sealed class CreateSharedGroupUseCaseTests
{
    private static (
        InMemoryQueryRepositories repos,
        IGroupRegistrationPort registration,
        InMemorySharedGroupStateRepository sharedState,
        ISecureKeyStoragePort keyStorage,
        IAuthPort auth,
        CreateSharedGroupUseCase sut
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

        var sut = new CreateSharedGroupUseCase(
            repos,
            registration,
            sharedState,
            keyStorage,
            auth,
            new SequentialIdGenerator());

        return (repos, registration, sharedState, keyStorage, auth, sut);
    }

    [Fact]
    public async Task HappyPath_CreatesGroupAndPersistsSharedState()
    {
        var (repos, registration, sharedState, _, _, sut) = BuildSut();

        var groupId = await sut.ExecuteAsync("EUR", "device-1", CancellationToken.None);

        Assert.NotNull(groupId);
        Assert.NotEmpty(repos.Groups);
        Assert.Equal(groupId, repos.Groups[0].Id);
        Assert.Equal("EUR", repos.Groups[0].Currency);

        var state = await sharedState.GetByGroupIdAsync(groupId, CancellationToken.None);
        Assert.NotNull(state);
        Assert.True(state!.IsShared);
        Assert.Equal("user-1", state.OwnerId);

        await registration.Received(1).RegisterGroupAsync(
            Arg.Is<CreateGroupRequest>(r => r.OwnerId == "user-1" && r.OwnerDeviceId == "device-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_ThrowsValidationError()
    {
        var (_, _, _, _, _, sut) = BuildSut(userId: null);

        await Assert.ThrowsAsync<ValidationError>(() =>
            sut.ExecuteAsync("EUR", "device-1", CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateGroup_RegistrationThrows_PropagatesException()
    {
        var (_, registration, _, _, _, sut) = BuildSut();
        registration.RegisterGroupAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Group already registered."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync("EUR", "device-1", CancellationToken.None));
    }
}

internal sealed class InMemorySharedGroupStateRepository : ISharedGroupStateRepository
{
    private readonly Dictionary<string, SharedGroupState> _store = new();

    public Task<SharedGroupState?> GetByGroupIdAsync(string groupId, CancellationToken ct)
    {
        _store.TryGetValue(groupId, out var state);
        return Task.FromResult(state);
    }

    public Task SaveAsync(string groupId, SharedGroupState state, CancellationToken ct)
    {
        _store[groupId] = state;
        return Task.CompletedTask;
    }
}
