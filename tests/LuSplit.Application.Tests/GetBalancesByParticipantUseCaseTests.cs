using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class GetBalancesByParticipantUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsZeroSumBalances()
    {
        var repos = new InMemoryQueryRepositories();
        SeedGroup(repos, "g1");
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));
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

        var useCase = new GetBalancesByParticipantUseCase(repos, repos, repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        Assert.Equal(
            new[]
            {
                new BalanceModel("p1", 50),
                new BalanceModel("p2", -50)
            },
            result);
    }

    [Fact]
    public async Task ExecuteAsyncFailsForUnknownGroup()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetBalancesByParticipantUseCase(repos, repos, repos, repos);

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync("missing"));

        Assert.Equal("Group not found: missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetBalancesByParticipantUseCase(repos, repos, repos, repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync("  "));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncSubtractsRecordedPaymentsFromWhatPeopleOwe()
    {
        var repos = new InMemoryQueryRepositories();
        SeedGroup(repos, "g1");
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));
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
        repos.Transfers.Add(new Transfer("t1", "g1", "p2", "p1", 20, "2026-01-02", TransferType.Manual, null));

        var useCase = new GetBalancesByParticipantUseCase(repos, repos, repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        Assert.Equal(
            new[]
            {
                new BalanceModel("p1", 30),
                new BalanceModel("p2", -30)
            },
            result);
    }

    private static void SeedGroup(InMemoryQueryRepositories repos, string id)
    {
        repos.Groups.Add(new Group(id, "USD", false));
    }
}
