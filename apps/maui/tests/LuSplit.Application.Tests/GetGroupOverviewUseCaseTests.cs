using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

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
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
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
}
