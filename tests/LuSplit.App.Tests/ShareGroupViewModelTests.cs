using LuSplit.App.Features.SharedGroups;
using LuSplit.Application.Groups.UseCases;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Ports;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class ShareGroupViewModelTests
{
    private static (ShareGroupViewModel vm, CreateSharedGroupUseCase useCase) Build(
        Func<string, string, CancellationToken, Task<string>>? executeImpl = null)
    {
        var registrationPort = Substitute.For<IGroupRegistrationPort>();
        var sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns("user-1");

        registrationPort.RegisterGroupAsync(Arg.Any<LuSplit.Contracts.ControlPlane.CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var req = call.Arg<LuSplit.Contracts.ControlPlane.CreateGroupRequest>();
                return Task.FromResult(new LuSplit.Contracts.ControlPlane.CreateGroupResponse(req.GroupId, $"grp-{req.GroupId}"));
            });

        var repos = new LuSplit.Application.Tests.Fakes.InMemoryQueryRepositories();
        var idGen = new LuSplit.Application.Tests.Fakes.SequentialIdGenerator();

        var useCase = new CreateSharedGroupUseCase(repos, registrationPort, sharedStateRepo, keyStorage, auth, idGen);
        var vm = new ShareGroupViewModel(useCase);
        vm.Initialize("device-1");

        return (vm, useCase);
    }

    [Fact]
    public async Task CreateSharedGroupCommand_SetsIsLoadingDuringExecution()
    {
        var (vm, _) = Build();
        bool wasLoading = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
                wasLoading = true;
        };

        await vm.CreateSharedGroupCommand.ExecuteAsync(null);

        Assert.True(wasLoading);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task CreateSharedGroupCommand_OnSuccess_RaisesGroupCreatedEvent()
    {
        var (vm, _) = Build();
        string? createdGroupId = null;
        vm.GroupCreated += (_, id) => createdGroupId = id;

        await vm.CreateSharedGroupCommand.ExecuteAsync(null);

        Assert.NotNull(createdGroupId);
    }

    [Fact]
    public async Task CreateSharedGroupCommand_OnError_SetsErrorMessage()
    {
        var registrationPort = Substitute.For<IGroupRegistrationPort>();
        registrationPort.RegisterGroupAsync(Arg.Any<LuSplit.Contracts.ControlPlane.CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Network error"));

        var sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();
        var keyStorage = Substitute.For<ISecureKeyStoragePort>();
        var auth = Substitute.For<IAuthPort>();
        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns("user-1");

        var repos = new LuSplit.Application.Tests.Fakes.InMemoryQueryRepositories();
        var idGen = new LuSplit.Application.Tests.Fakes.SequentialIdGenerator();
        var useCase = new CreateSharedGroupUseCase(repos, registrationPort, sharedStateRepo, keyStorage, auth, idGen);
        var vm = new ShareGroupViewModel(useCase);
        vm.Initialize("device-1");

        await vm.CreateSharedGroupCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }
}
