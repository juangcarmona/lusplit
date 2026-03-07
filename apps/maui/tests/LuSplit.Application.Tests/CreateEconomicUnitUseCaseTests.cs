using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
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

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateEconomicUnitInput("g1", "p1")));
    }
}
