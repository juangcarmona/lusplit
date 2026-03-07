using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class DeleteExpenseUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncDeletesAnExistingExpense()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Expenses.Add(new Expense(
            "e1",
            "g1",
            "Dinner",
            "p1",
            100,
            "2026-01-01",
            new SplitDefinition(Array.Empty<SplitComponent>())));

        var useCase = new DeleteExpenseUseCase(repos, repos);

        await useCase.ExecuteAsync(new DeleteExpenseInput("g1", "e1"));

        Assert.Null(await repos.GetExpenseByIdAsync("e1", CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsyncFailsWhenExpenseDoesNotExist()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));

        var useCase = new DeleteExpenseUseCase(repos, repos);

        await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new DeleteExpenseInput("g1", "missing")));
    }
}
