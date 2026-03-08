using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class AddExpenseUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncStoresExpenseWithProvidedSplitDefinition()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));

        var useCase = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var result = await useCase.ExecuteAsync(new AddExpenseInput(
            GroupId: "g1",
            Title: "Dinner",
            PaidByParticipantId: "p1",
            AmountMinor: 100,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2" }, RemainderMode.Equal)
            })));

        Assert.Equal("id-1", result.Id);
        Assert.Equal("REMAINDER", result.SplitDefinition.Components[0] is RemainderSplitComponent ? "REMAINDER" : "OTHER");
    }

    [Fact]
    public async Task ExecuteAsyncValidatesPayerInGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));

        var useCase = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddExpenseInput(
            GroupId: "g1",
            Title: "Dinner",
            PaidByParticipantId: "p2",
            AmountMinor: 100,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            }))));

        Assert.Equal("Payer is not in group g1", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddExpenseInput(
            GroupId: "  ",
            Title: "Dinner",
            PaidByParticipantId: "p1",
            AmountMinor: 100,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            }))));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsNonPositiveAmountWithParityMessage()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));

        var useCase = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddExpenseInput(
            GroupId: "g1",
            Title: "Dinner",
            PaidByParticipantId: "p1",
            AmountMinor: 0,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            }))));

        Assert.Equal("amountMinor must be greater than zero", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsInvalidIsoDate()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));

        var useCase = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddExpenseInput(
            GroupId: "g1",
            Title: "Dinner",
            PaidByParticipantId: "p1",
            AmountMinor: 100,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            }),
            Date: "not-a-date")));

        Assert.Equal("date must be a valid ISO date", error.Message);
    }
}
