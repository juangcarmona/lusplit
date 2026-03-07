using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Errors;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class GetBalancesByEconomicUnitOwnerUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncAggregatesBalancesByOwner()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1", "Unit 1"));
        repos.EconomicUnits.Add(new EconomicUnit("u2", "g1", "p2", "Unit 2"));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p3", "g1", "u2", "P3", ConsumptionCategory.Full));
        repos.Expenses.Add(new Expense(
            "e1",
            "g1",
            "Dinner",
            "p1",
            90,
            "2026-01-01",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2", "p3" }, RemainderMode.Equal)
            })));

        var useCase = new GetBalancesByEconomicUnitOwnerUseCase(repos, repos, repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        Assert.Equal(
            new[]
            {
                new BalanceModel("p1", 60),
                new BalanceModel("p2", -60)
            },
            result);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesOwnerUnitRelationship()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p2", "Broken Unit"));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));

        var useCase = new GetBalancesByEconomicUnitOwnerUseCase(repos, repos, repos, repos);

        await Assert.ThrowsAsync<DomainInvariantException>(() => useCase.ExecuteAsync("g1"));
    }
}
