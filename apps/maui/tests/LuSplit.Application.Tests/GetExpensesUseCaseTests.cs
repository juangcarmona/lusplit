using LuSplit.Application.Errors;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class GetExpensesUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsMappedExpenses()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Expenses.Add(new Expense(
            "e1",
            "g1",
            "A",
            "p1",
            100,
            "2026-01-01",
            new SplitDefinition(Array.Empty<SplitComponent>())));
        repos.Expenses.Add(new Expense(
            "e2",
            "g1",
            "B",
            "p1",
            200,
            "2026-01-02",
            new SplitDefinition(Array.Empty<SplitComponent>())));

        var useCase = new GetExpensesUseCase(repos, repos);

        var result = await useCase.ExecuteAsync("g1");

        Assert.Equal(2, result.Count);
        Assert.Equal("e1", result[0].Id);
        Assert.Equal("A", result[0].Title);
        Assert.Equal("e2", result[1].Id);
    }

    [Fact]
    public async Task ExecuteAsyncFailsForUnknownGroup()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetExpensesUseCase(repos, repos);

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync("missing"));

        Assert.Equal("Group not found: missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new GetExpensesUseCase(repos, repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync("  "));

        Assert.Equal("groupId is required", error.Message);
    }
}
