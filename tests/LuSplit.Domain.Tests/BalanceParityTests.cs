using LuSplit.Domain.Balance;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Errors;
using LuSplit.Domain.Split;

namespace LuSplit.Domain.Tests;

public sealed class BalanceParityTests
{
    private const string GroupId = "g1";
    private const string ParticipantA = "a";
    private const string ParticipantB = "b";
    private const string ParticipantC = "c";

    private static readonly Participant[] Participants =
    {
        new(ParticipantA, GroupId, "u1", "A", ConsumptionCategory.Full),
        new(ParticipantB, GroupId, "u2", "B", ConsumptionCategory.Full),
        new(ParticipantC, GroupId, "u2", "C", ConsumptionCategory.Full)
    };

    private static readonly EconomicUnit[] Units =
    {
        new("u1", GroupId, ParticipantA, "Unit 1"),
        new("u2", GroupId, ParticipantB, "Unit 2")
    };

    private static readonly Expense[] Expenses =
    {
        new(
            "e1",
            GroupId,
            "Dinner",
            ParticipantA,
            900,
            "2026-01-01",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { ParticipantA, ParticipantB, ParticipantC }, RemainderMode.Equal)
            })),
        new(
            "e2",
            GroupId,
            "Taxi",
            ParticipantB,
            600,
            "2026-01-02",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { ParticipantA, ParticipantB }, RemainderMode.Equal)
            }))
    };

    private static readonly Transfer[] Transfers = Array.Empty<Transfer>();

    [Fact]
    public void CalculatesParticipantBalancesWithZeroSumInvariant()
    {
        var balances = BalanceCalculator.CalculateParticipantBalances(Expenses, Transfers, Participants);

        Assert.Equal(300, balances[ParticipantA]);
        Assert.Equal(0, balances[ParticipantB]);
        Assert.Equal(-300, balances[ParticipantC]);
        Assert.Equal(0, balances.Values.Sum());
    }

    [Fact]
    public void ThrowsWhenExpenseGroupDiffersFromParticipantsGroup()
    {
        var mismatchedExpenses = new[]
        {
            Expenses[0] with { GroupId = "other-group" }
        };

        Assert.Throws<DomainInvariantException>(() =>
            BalanceCalculator.CalculateParticipantBalances(mismatchedExpenses, Transfers, Participants));
    }

    [Fact]
    public void AggregatesBalancesByEconomicUnitOwner()
    {
        var balances = new Dictionary<string, long>
        {
            [ParticipantA] = 400,
            [ParticipantB] = -100,
            [ParticipantC] = -300
        };

        var aggregated = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(balances, Participants, Units);

        Assert.Equal(400, aggregated[ParticipantA]);
        Assert.Equal(-400, aggregated[ParticipantB]);
    }

    [Fact]
    public void ThrowsWhenUnitOwnerDoesNotBelongToItsUnit()
    {
        var brokenUnits = new[]
        {
            new EconomicUnit("u1", GroupId, ParticipantB, "Broken Unit")
        };

        Assert.Throws<DomainInvariantException>(() =>
            BalanceCalculator.AggregateBalancesByEconomicUnitOwner(
                new Dictionary<string, long> { [ParticipantA] = 0 },
                Participants,
                brokenUnits));
    }

    [Fact]
    public void ThrowsWhenEconomicUnitsAndParticipantsAreFromDifferentGroups()
    {
        var wrongGroupUnits = new[]
        {
            new EconomicUnit("u1", "g2", ParticipantA, "Wrong Group Unit")
        };

        Assert.Throws<DomainInvariantException>(() =>
            BalanceCalculator.AggregateBalancesByEconomicUnitOwner(
                new Dictionary<string, long> { [ParticipantA] = 0 },
                Participants,
                wrongGroupUnits));
    }
}
