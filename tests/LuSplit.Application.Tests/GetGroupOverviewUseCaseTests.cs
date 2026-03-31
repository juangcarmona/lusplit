using LuSplit.Application.Groups.Queries;
using LuSplit.Application.Payments.Models;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Payments;

namespace LuSplit.Application.Tests;

public sealed class GetGroupOverviewUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsComposedViewModel()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u1", "Bob", ConsumptionCategory.Full));
        repos.Expenses.Add(new Expense(
            "e1",
            "g1",
            "Dinner",
            "p1",
            100,
            "2026-01-01",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2" }, RemainderMode.Equal)
            })));
        repos.Transfers.Add(new Transfer(
            "t1",
            "g1",
            "p2",
            "p1",
            10,
            "2026-01-02",
            TransferType.Manual,
            null));

        var useCase = new GetGroupOverviewUseCase(repos, repos, repos, repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        Assert.Equal("g1", result.Group.Id);
        Assert.Equal(1, result.Summary.ExpenseCount);
        Assert.Equal(1, result.Summary.TransferCount);
        Assert.Equal(SettlementMode.Participant, result.SettlementByParticipant.Mode);
        Assert.Equal("e1", result.Expenses[0].Id);
        Assert.Equal("t1", result.Transfers[0].Id);
        Assert.Equal(new[] { new BalanceModel("p1", 40), new BalanceModel("p2", -40) }, result.BalancesByParticipant);
    }

    [Fact]
    public async Task ExecuteAsyncFailsForMissingGroup()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetGroupOverviewUseCase(repos, repos, repos, repos, repos);

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync("missing"));

        Assert.Equal("Group not found: missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetGroupOverviewUseCase(repos, repos, repos, repos, repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync("  "));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncAggregatesEconomicUnitOwnerBalancesAndSettlement()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));  // Alice alone
        repos.EconomicUnits.Add(new EconomicUnit("u2", "g1", "p2"));  // Bob + Charlie
        repos.Participants.Add(new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "Bob", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p3", "g1", "u2", "Charlie", ConsumptionCategory.Full));
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

        var useCase = new GetGroupOverviewUseCase(repos, repos, repos, repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        // p1 paid 90, owes 30 → net +60; p2 owes 30, p3 owes 30 → u2 owner p2 net -60
        Assert.Equal(
            new[] { new BalanceModel("p1", 60), new BalanceModel("p2", -60) },
            result.BalancesByEconomicUnitOwner);

        Assert.Equal(SettlementMode.EconomicUnitOwner, result.SettlementByEconomicUnitOwner.Mode);
        Assert.Equal(
            new[] { new SettlementTransferModel("p2", "p1", 60) },
            result.SettlementByEconomicUnitOwner.Transfers);
    }
}
