using LuSplit.Application.Groups.Commands;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Tests;

public sealed class CreateEconomicUnitUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncCreatesEconomicUnitInOpenGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var result = await useCase.ExecuteAsync(new CreateEconomicUnitInput("g1", "p1", "Household"));

        Assert.Equal("id-1", result.Id);
        Assert.Equal("g1", result.GroupId);
    }

    [Fact]
    public async Task ExecuteAsyncFailsWhenGroupIsClosed()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", true));
        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateEconomicUnitInput("g1", "p1")));

        Assert.Equal("Group is closed: g1", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateEconomicUnitInput("  ", "p1")));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresOwnerParticipantId()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateEconomicUnitInput("g1", " ")));

        Assert.Equal("ownerParticipantId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncErrorsWhenGroupDoesNotExist()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new CreateEconomicUnitInput("missing", "p1")));

        Assert.Equal("Group not found: missing", error.Message);
    }
}
