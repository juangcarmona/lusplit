using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class EditExpenseUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncUpdatesExpenseFieldsAndValidatesSplit()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));
        repos.Expenses.Add(new Expense(
            "e1",
            "g1",
            "Original",
            "p1",
            100,
            "2026-01-01",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            })));

        var useCase = new EditExpenseUseCase(repos, repos, repos);

        var result = await useCase.ExecuteAsync(new EditExpenseInput(
            GroupId: "g1",
            ExpenseId: "e1",
            Title: "Edited",
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2" }, RemainderMode.Equal)
            })));

        Assert.Equal("Edited", result.Title);
        Assert.True(result.SplitDefinition.Components[0] is RemainderSplitComponent);
    }

    [Fact]
    public async Task ExecuteAsyncFailsForUnknownExpense()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));

        var useCase = new EditExpenseUseCase(repos, repos, repos);

        await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new EditExpenseInput(
            GroupId: "g1",
            ExpenseId: "missing")));
    }
}
