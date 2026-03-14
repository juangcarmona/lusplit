using LuSplit.Domain.Errors;
using LuSplit.Domain.Settlement;

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
}
