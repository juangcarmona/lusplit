using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Payments;
using LuSplit.Domain.Shared;

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

    [Fact]
    public void AggregatesDependentChildrenDebtUnderResponsibleOwner()
    {
        const string grand = "grand";
        const string juan = "juan";
        const string juanito = "juanito";
        const string julia = "julia";

        var participants = new[]
        {
            new Participant(grand, GroupId, "u-grand", "Grand", ConsumptionCategory.Full),
            new Participant(juan, GroupId, "u-juan", "Juan", ConsumptionCategory.Full),
            new Participant(juanito, GroupId, "u-juan", "Juanito", ConsumptionCategory.Full),
            new Participant(julia, GroupId, "u-juan", "Julia", ConsumptionCategory.Full)
        };

        var units = new[]
        {
            new EconomicUnit("u-grand", GroupId, grand, "Grand"),
            new EconomicUnit("u-juan", GroupId, juan, "Juan Family")
        };

        var expenses = new[]
        {
            new Expense(
                "e-children-sweets",
                GroupId,
                "Sweets for kids",
                grand,
                900,
                "2026-01-03",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { juanito, julia }, RemainderMode.Equal)
                }))
        };

        var balances = BalanceCalculator.CalculateParticipantBalances(expenses, Array.Empty<Transfer>(), participants);

        Assert.Equal(900, balances[grand]);
        Assert.Equal(0, balances[juan]);
        Assert.Equal(-450, balances[juanito]);
        Assert.Equal(-450, balances[julia]);

        var aggregated = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(balances, participants, units);

        Assert.Equal(2, aggregated.Count);
        Assert.Equal(900, aggregated[grand]);
        Assert.Equal(-900, aggregated[juan]);
    }

    [Fact]
    public void WeekendScenario_S1_ChildrenDebtIsAssumedByResponsibleOwners()
    {
        var participants = CreateWeekendParticipants(childrenAreHalf: false);
        var units = CreateWeekendUnits();

        var expenses = new[]
        {
            new Expense(
                "e-s1",
                GroupId,
                "Dinner",
                "juan",
                12_000,
                "2026-01-03",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "juan", "elena", "gema", "manuela", "juanito", "julia" }, RemainderMode.Equal)
                }))
        };

        var balances = BalanceCalculator.CalculateParticipantBalances(expenses, Array.Empty<Transfer>(), participants);
        var aggregated = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(balances, participants, units);

        Assert.Equal(10_000, balances["juan"]);
        Assert.Equal(-2_000, balances["elena"]);
        Assert.Equal(-2_000, balances["gema"]);
        Assert.Equal(-2_000, balances["manuela"]);
        Assert.Equal(-2_000, balances["juanito"]);
        Assert.Equal(-2_000, balances["julia"]);

        Assert.Equal(8_000, aggregated["juan"]);
        Assert.Equal(-4_000, aggregated["elena"]);
        Assert.Equal(-4_000, aggregated["gema"]);
    }

    [Fact]
    public void WeekendScenario_S2_ManualTransferRepresentsSplitPayment()
    {
        var participants = CreateWeekendParticipants(childrenAreHalf: false);
        var units = CreateWeekendUnits();

        var expenses = new[]
        {
            new Expense(
                "e-s2",
                GroupId,
                "Dinner",
                "juan",
                12_000,
                "2026-01-03",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "juan", "elena", "gema", "manuela", "juanito", "julia" }, RemainderMode.Equal)
                }))
        };

        var transfers = new[]
        {
            new Transfer("t-s2", GroupId, "gema", "juan", 6_000, "2026-01-03", TransferType.Manual)
        };

        var balances = BalanceCalculator.CalculateParticipantBalances(expenses, transfers, participants);
        var aggregated = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(balances, participants, units);

        Assert.Equal(4_000, balances["juan"]);
        Assert.Equal(4_000, balances["gema"]);
        Assert.Equal(-2_000, balances["elena"]);

        Assert.Equal(2_000, aggregated["juan"]);
        Assert.Equal(-4_000, aggregated["elena"]);
        Assert.Equal(2_000, aggregated["gema"]);
    }

    [Fact]
    public void WeekendScenario_S3_WeightedChildrenAtHalfShareKeepsOwnerResponsibility()
    {
        var participants = CreateWeekendParticipants(childrenAreHalf: true);
        var units = CreateWeekendUnits();

        var expenses = new[]
        {
            new Expense(
                "e-s3",
                GroupId,
                "Dinner",
                "juan",
                12_000,
                "2026-01-03",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        new[] { "juan", "elena", "gema", "manuela", "juanito", "julia" },
                        RemainderMode.Weight)
                }))
        };

        var balances = BalanceCalculator.CalculateParticipantBalances(expenses, Array.Empty<Transfer>(), participants);
        var aggregated = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(balances, participants, units);

        Assert.Equal(9_333, balances["juan"]);
        Assert.Equal(-2_667, balances["elena"]);
        Assert.Equal(-2_667, balances["gema"]);
        Assert.Equal(-1_333, balances["manuela"]);
        Assert.Equal(-1_333, balances["juanito"]);
        Assert.Equal(-1_333, balances["julia"]);

        Assert.Equal(8_000, aggregated["juan"]);
        Assert.Equal(-4_000, aggregated["elena"]);
        Assert.Equal(-4_000, aggregated["gema"]);
    }

    private static Participant[] CreateWeekendParticipants(bool childrenAreHalf)
        => new[]
        {
            new Participant("juan", GroupId, "u-juan", "Juan", ConsumptionCategory.Full),
            new Participant("elena", GroupId, "u-elena", "Elena", ConsumptionCategory.Full),
            new Participant("gema", GroupId, "u-gema", "Gema", ConsumptionCategory.Full),
            new Participant("manuela", GroupId, "u-elena", "Manuela", childrenAreHalf ? ConsumptionCategory.Half : ConsumptionCategory.Full),
            new Participant("juanito", GroupId, "u-juan", "Juanito", childrenAreHalf ? ConsumptionCategory.Half : ConsumptionCategory.Full),
            new Participant("julia", GroupId, "u-gema", "Julia", childrenAreHalf ? ConsumptionCategory.Half : ConsumptionCategory.Full)
        };

    private static EconomicUnit[] CreateWeekendUnits()
        => new[]
        {
            new EconomicUnit("u-juan", GroupId, "juan", "Juan family"),
            new EconomicUnit("u-elena", GroupId, "elena", "Elena family"),
            new EconomicUnit("u-gema", GroupId, "gema", "Gema family")
        };
}
