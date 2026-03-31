using LuSplit.Domain.Payments;
using LuSplit.Domain.Shared;

namespace LuSplit.Domain.Tests;

public sealed class SettlementParityTests
{
    [Fact]
    public void CreatesDeterministicSettlementTransfers()
    {
        var balances = new Dictionary<string, long>
        {
            ["a"] = 500,
            ["b"] = 200,
            ["c"] = -300,
            ["d"] = -400
        };

        var transfers = SettlementPlanner.PlanSettlement(balances);

        var expected = new[]
        {
            new SettlementTransfer("c", "a", 300),
            new SettlementTransfer("d", "a", 200),
            new SettlementTransfer("d", "b", 200)
        };

        Assert.Equal(expected, transfers);
    }

    [Fact]
    public void ThrowsWhenBalancesAreNotZeroSum()
    {
        Assert.Throws<DomainInvariantException>(() =>
            SettlementPlanner.PlanSettlement(new Dictionary<string, long> { ["a"] = 1 }));
    }

    [Fact]
    public void ReturnsEmptyListWhenAllBalancesAreZero()
    {
        var balances = new Dictionary<string, long>
        {
            ["a"] = 0,
            ["b"] = 0
        };

        var transfers = SettlementPlanner.PlanSettlement(balances);

        Assert.Empty(transfers);
    }

    [Fact]
    public void ProducesOneTransferForSingleCreditorAndSingleDebtor()
    {
        var balances = new Dictionary<string, long>
        {
            ["a"] = 100,
            ["b"] = -100
        };

        var transfers = SettlementPlanner.PlanSettlement(balances);

        Assert.Equal(new[] { new SettlementTransfer("b", "a", 100) }, transfers);
    }
}
