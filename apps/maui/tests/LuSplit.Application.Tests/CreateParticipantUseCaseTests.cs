using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Tests;

public sealed class CreateParticipantUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncCreatesParticipantInExistingEconomicUnit()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var result = await useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Full));

        Assert.Equal("p1", result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public async Task ExecuteAsyncGeneratesNewIdAfterOwnerParticipantExistsInUnit()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var owner = await useCase.ExecuteAsync(new CreateParticipantInput("g1", "u1", "Alice", ConsumptionCategory.Full));
        var another = await useCase.ExecuteAsync(new CreateParticipantInput("g1", "u1", "Bob", ConsumptionCategory.Full));

        Assert.Equal("p1", owner.Id);
        Assert.Equal("id-1", another.Id);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesCustomWeightForCustomCategory()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Custom)));
    }
}
