using LuSplit.Application.Expenses.Commands;
using LuSplit.Application.Groups.Commands;
using LuSplit.Application.Groups.Queries;
using LuSplit.Application.Payments.Queries;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Tests;

/// <summary>
/// Tests for the archive (close) flow.
///
/// Design note: the domain reuses the <see cref="Group.Closed"/> flag as the archived state.
/// "Closed" and "archived" are synonymous. A closed group is visible in the archived list
/// and is read-only: the application layer blocks new expenses and participants on closed groups.
/// </summary>
public sealed class ArchiveGroupFlowTests
{
    [Fact]
    public async Task ArchivingAGroupSetsClosedFlagOnGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));

        var result = await new CloseGroupUseCase(repos).ExecuteAsync(new CloseGroupInput("g1"));

        Assert.True(result.Closed);
        Assert.True(repos.Groups.Single(g => g.Id == "g1").Closed);
    }

    [Fact]
    public async Task AddingParticipantToArchivedGroupIsRejected()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", true)); // already archived
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() =>
            useCase.ExecuteAsync(new CreateParticipantInput("g1", "u1", "Alice", ConsumptionCategory.Full)));

        Assert.Contains("closed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddingEconomicUnitToArchivedGroupIsRejected()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", true));

        var useCase = new CreateEconomicUnitUseCase(repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() =>
            useCase.ExecuteAsync(new CreateEconomicUnitInput("g1", "id-1")));

        Assert.Contains("closed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArchivedGroupGroupOverviewIsStillReadable()
    {
        // Archived groups can be read (for export, inspection); the group remains in the repository.
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", true));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full));

        var overview = await new GetGroupOverviewUseCase(repos, repos, repos, repos, repos).ExecuteAsync("g1");

        Assert.True(overview.Group.Closed);
        Assert.Single(overview.Participants);
    }

    [Fact]
    public async Task ArchivingAlreadyArchivedGroupIsIdempotent()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));

        var useCase = new CloseGroupUseCase(repos);
        var first = await useCase.ExecuteAsync(new CloseGroupInput("g1"));
        var second = await useCase.ExecuteAsync(new CloseGroupInput("g1"));

        Assert.True(first.Closed);
        Assert.True(second.Closed);
    }

    [Fact]
    public async Task FamilySettlementAggregatesDependentsUnderOwner()
    {
        // Regression: verify that economic-unit owner aggregation correctly merges
        // a dependent's balance into the household payer's balance.
        var repos = new InMemoryQueryRepositories();
        var idGen = new SequentialIdGenerator();

        var group = await new CreateGroupUseCase(repos, idGen).ExecuteAsync(new CreateGroupInput("EUR"));

        // Alice is the household payer; Bob is her dependent child (half share).
        var unit1 = await new CreateEconomicUnitUseCase(repos, repos, idGen).ExecuteAsync(
            new CreateEconomicUnitInput(group.Id, "id-4", "Smith Family"));

        var alice = await new CreateParticipantUseCase(repos, repos, repos, idGen).ExecuteAsync(
            new CreateParticipantInput(group.Id, unit1.Id, "Alice", ConsumptionCategory.Full));

        var bob = await new CreateParticipantUseCase(repos, repos, repos, idGen).ExecuteAsync(
            new CreateParticipantInput(group.Id, unit1.Id, "Bob", ConsumptionCategory.Half));

        // Charlie is fully independent.
        var unit2 = await new CreateEconomicUnitUseCase(repos, repos, idGen).ExecuteAsync(
            new CreateEconomicUnitInput(group.Id, "id-5", "Jones"));

        var charlie = await new CreateParticipantUseCase(repos, repos, repos, idGen).ExecuteAsync(
            new CreateParticipantInput(group.Id, unit2.Id, "Charlie", ConsumptionCategory.Full));

        // Charlie pays for a $90 dinner shared equally among Alice, Bob (half), and Charlie.
        await new AddExpenseUseCase(repos, repos, repos, idGen, new FixedClock("2026-01-01T00:00:00Z")).ExecuteAsync(
            new AddExpenseInput(
                GroupId: group.Id,
                Title: "Dinner",
                PaidByParticipantId: charlie.Id,
                AmountMinor: 9000,
                SplitDefinition: new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        new[] { alice.Id, bob.Id, charlie.Id },
                        RemainderMode.Equal)
                })));

        var ownerBalances = await new GetBalancesByEconomicUnitOwnerUseCase(repos, repos, repos, repos, repos)
            .ExecuteAsync(group.Id);

        // Alice owns a unit that includes Bob (half-weight dependent).
        // Owner-level balance aggregates Bob's balance into Alice's.
        // There are only two settlement entities at owner level: Alice and Charlie.
        Assert.Equal(2, ownerBalances.Count);

        var aliceBalance = ownerBalances.Single(b => string.Equals(b.EntityId, alice.Id, StringComparison.Ordinal));
        var charlieBalance = ownerBalances.Single(b => string.Equals(b.EntityId, charlie.Id, StringComparison.Ordinal));

        // The sum must be zero (invariant).
        Assert.Equal(0, aliceBalance.AmountMinor + charlieBalance.AmountMinor);

        // Charlie paid more than he owed → positive balance.
        Assert.True(charlieBalance.AmountMinor > 0);
        // Alice's unit owes money → negative aggregate balance.
        Assert.True(aliceBalance.AmountMinor < 0);
    }
}
