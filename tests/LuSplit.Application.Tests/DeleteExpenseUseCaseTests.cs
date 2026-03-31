using LuSplit.Application.Expenses.Commands;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;

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

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new DeleteExpenseInput("g1", "missing")));

        Assert.Equal("Expense not found: missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new DeleteExpenseUseCase(repos, repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new DeleteExpenseInput("  ", "e1")));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresExpenseId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new DeleteExpenseUseCase(repos, repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new DeleteExpenseInput("g1", "  ")));

        Assert.Equal("expenseId is required", error.Message);
    }
}
