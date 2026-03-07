using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class GetSettlementPlanUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsDeterministicParticipantAndOwnerModePlans()
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

        var useCase = new GetSettlementPlanUseCase(repos, repos, repos, repos);

        var participantPlan = await useCase.ExecuteAsync("g1", SettlementMode.Participant);
        var ownerPlan = await useCase.ExecuteAsync("g1", SettlementMode.EconomicUnitOwner);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel("p2", "p1", 30),
                new SettlementTransferModel("p3", "p1", 30)
            },
            participantPlan.Transfers);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel("p2", "p1", 60)
            },
            ownerPlan.Transfers);
    }

    [Fact]
    public async Task ExecuteAsyncErrorsForInvalidGroup()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetSettlementPlanUseCase(repos, repos, repos, repos);

        await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync("missing", SettlementMode.Participant));
    }
}
