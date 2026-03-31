using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Shared;

namespace LuSplit.Domain.Tests;

public sealed class SplitParityTests
{
    private const string GroupId = "g1";
    private const string ParticipantA = "a";
    private const string ParticipantB = "b";
    private const string ParticipantC = "c";

    private static readonly Participant[] Participants =
    {
        new(ParticipantA, GroupId, "u1", "A", ConsumptionCategory.Full),
        new(ParticipantB, GroupId, "u2", "B", ConsumptionCategory.Half),
        new(ParticipantC, GroupId, "u3", "C", ConsumptionCategory.Full)
    };

    [Fact]
    public void EvaluatesFixedThenRemainderSequentially()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                1_000,
                new SplitDefinition(new SplitComponent[]
                {
                    new FixedSplitComponent(new Dictionary<string, long> { [ParticipantA] = 300 }),
                    new RemainderSplitComponent(new[] { ParticipantB, ParticipantC }, RemainderMode.Equal)
                })),
            Participants);

        Assert.Equal(300, shares[ParticipantA]);
        Assert.Equal(350, shares[ParticipantB]);
        Assert.Equal(350, shares[ParticipantC]);
        Assert.Equal(1_000, shares.Values.Sum());
    }

    [Fact]
    public void UsesDeterministicRoundingByParticipantOrdering()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                10,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { ParticipantC, ParticipantA, ParticipantB }, RemainderMode.Equal)
                })),
            Participants);

        Assert.Equal(4, shares[ParticipantA]);
        Assert.Equal(3, shares[ParticipantB]);
        Assert.Equal(3, shares[ParticipantC]);
    }

    [Fact]
    public void SupportsWeightModeFromParticipantCategories()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                7,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { ParticipantA, ParticipantB }, RemainderMode.Weight)
                })),
            Participants);

        Assert.Equal(5, shares[ParticipantA]);
        Assert.Equal(2, shares[ParticipantB]);
    }

    [Fact]
    public void SupportsPercentModeAndConsumesFullRemainder()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                101,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        new[] { ParticipantA, ParticipantB },
                        RemainderMode.Percent,
                        Percents: new Dictionary<string, int>
                        {
                            [ParticipantA] = 50,
                            [ParticipantB] = 50
                        })
                })),
            Participants);

        Assert.Equal(51, shares[ParticipantA]);
        Assert.Equal(50, shares[ParticipantB]);
    }

    [Fact]
    public void SingleParticipantCanConsumeAllRemainder()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                250,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { ParticipantA }, RemainderMode.Equal)
                })),
            Participants);

        Assert.Equal(250, shares[ParticipantA]);
        Assert.Equal(0, shares[ParticipantB]);
        Assert.Equal(0, shares[ParticipantC]);
    }

    [Fact]
    public void ThrowsIfSplitDoesNotConsumeFullExpenseAmount()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(100, new SplitDefinition(Array.Empty<SplitComponent>())),
                Participants));
    }

    [Fact]
    public void HandlesEmptyParticipantsWithZeroAmount()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense("g1", 0, new SplitDefinition(Array.Empty<SplitComponent>())),
            Array.Empty<Participant>());

        Assert.Empty(shares);
    }

    [Fact]
    public void ThrowsWhenParticipantsAreDuplicatedInRemainder()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(new[] { ParticipantA, ParticipantA }, RemainderMode.Equal)
                    })),
                Participants));
    }

    [Fact]
    public void ThrowsWhenSplitParticipantsContainAnotherGroup()
    {
        const string differentGroupParticipantId = "out";

        var extendedParticipants = Participants
            .Concat(new[]
            {
                new Participant(differentGroupParticipantId, "g2", "u-out", "Out", ConsumptionCategory.Full)
            })
            .ToArray();

        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(new[] { ParticipantA, differentGroupParticipantId }, RemainderMode.Equal)
                    })),
                extendedParticipants));
    }

    [Fact]
    public void ThrowsWhenFixedSharesExceedRemainingAmount()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new FixedSplitComponent(new Dictionary<string, long>
                        {
                            [ParticipantA] = 101
                        })
                    })),
                Participants));
    }

    [Fact]
    public void ThrowsWhenPercentSumIsNotExactlyHundred()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(
                            new[] { ParticipantA, ParticipantB },
                            RemainderMode.Percent,
                            Percents: new Dictionary<string, int>
                            {
                                [ParticipantA] = 70,
                                [ParticipantB] = 20
                            })
                    })),
                Participants));
    }

    [Fact]
    public void ThrowsWhenPercentModeIsMissingPercentMap()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(new[] { ParticipantA, ParticipantB }, RemainderMode.Percent)
                    })),
                Participants));
    }

    [Fact]
    public void ThrowsWhenCustomWeightPrecisionExceedsSixDecimals()
    {
        var customParticipants = new[]
        {
            new Participant(ParticipantA, GroupId, "u1", "A", ConsumptionCategory.Custom, "1.1234567"),
            new Participant(ParticipantB, GroupId, "u2", "B", ConsumptionCategory.Full)
        };

        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    10,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(new[] { ParticipantA, ParticipantB }, RemainderMode.Weight)
                    })),
                customParticipants));
    }

    [Fact]
    public void UsesDeterministicTieBreakerForEqualRemaindersInWeightMode()
    {
        var participants = new[]
        {
            new Participant(ParticipantA, GroupId, "u1", "A", ConsumptionCategory.Custom, "1"),
            new Participant(ParticipantB, GroupId, "u2", "B", ConsumptionCategory.Custom, "1")
        };

        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                1,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { ParticipantB, ParticipantA }, RemainderMode.Weight)
                })),
            participants);

        Assert.Equal(1, shares[ParticipantA]);
        Assert.Equal(0, shares[ParticipantB]);
    }

    [Fact]
    public void UsesDeterministicTieBreakerForEqualRemaindersInPercentMode()
    {
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                1,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        new[] { ParticipantB, ParticipantA },
                        RemainderMode.Percent,
                        Percents: new Dictionary<string, int>
                        {
                            [ParticipantA] = 50,
                            [ParticipantB] = 50
                        })
                })),
            Participants);

        Assert.Equal(1, shares[ParticipantA]);
        Assert.Equal(0, shares[ParticipantB]);
    }

    [Fact]
    public void ThrowsWhenFixedShareIsNegative()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new FixedSplitComponent(new Dictionary<string, long>
                        {
                            [ParticipantA] = -1
                        })
                    })),
                Participants));
    }

    [Fact]
    public void ThrowsWhenExplicitWeightOverrideIsZero()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SplitEvaluator.EvaluateSplit(
                BuildExpense(
                    100,
                    new SplitDefinition(new SplitComponent[]
                    {
                        new RemainderSplitComponent(
                            new[] { ParticipantA, ParticipantB },
                            RemainderMode.Weight,
                            Weights: new Dictionary<string, string>
                            {
                                [ParticipantA] = "0",
                                [ParticipantB] = "1"
                            })
                    })),
                Participants));
    }

    [Fact]
    public void ExplicitWeightMapOverridesConsumptionCategory()
    {
        // ParticipantA is Full (default weight 1) and ParticipantB is Half (default weight 0.5).
        // Explicit weights reverse precedence: B gets weight 2, A gets weight 1.
        var shares = SplitEvaluator.EvaluateSplit(
            BuildExpense(
                9,
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        new[] { ParticipantA, ParticipantB },
                        RemainderMode.Weight,
                        Weights: new Dictionary<string, string>
                        {
                            [ParticipantA] = "1",
                            [ParticipantB] = "2"
                        })
                })),
            Participants);

        Assert.Equal(3, shares[ParticipantA]);  // 1/3 of 9
        Assert.Equal(6, shares[ParticipantB]);  // 2/3 of 9
    }

    private static Expense BuildExpense(long amountMinor, SplitDefinition splitDefinition)
        => BuildExpense(GroupId, amountMinor, splitDefinition);

    private static Expense BuildExpense(string groupId, long amountMinor, SplitDefinition splitDefinition)
        => new(
            "e1",
            groupId,
            "Expense",
            ParticipantA,
            amountMinor,
            "2026-01-01",
            splitDefinition);
}
