using LuSplit.Domain.Entities;
using LuSplit.Domain.Errors;
using LuSplit.Domain.Split;

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
