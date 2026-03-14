using System.Runtime.CompilerServices;
using System.Text.Json;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Errors;
using LuSplit.Domain.Money;
using LuSplit.Domain.Split;

namespace LuSplit.Domain.Tests;

public sealed class FoundationParityTests
{
    [Fact]
    public void RejectsFractionalMinorUnits()
    {
        Assert.Throws<DomainInvariantException>(() => MoneyAmount.FromMinorUnitsDecimal(12.5m));
    }

    [Fact]
    public void EqualSplitUsesDeterministicLexicalOrderingForRemainder()
    {
        var result = SplitEvaluator.EvaluateSplit(
            new Expense(
                "e1",
                "g1",
                "Test",
                "a",
                10,
                "2026-01-01",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "c", "a", "b" }, RemainderMode.Equal)
                })),
            new[]
            {
                new Participant("a", "g1", "u1", "A", ConsumptionCategory.Full),
                new Participant("b", "g1", "u2", "B", ConsumptionCategory.Full),
                new Participant("c", "g1", "u3", "C", ConsumptionCategory.Full)
            });

        Assert.Equal(4, result["a"]);
        Assert.Equal(3, result["b"]);
        Assert.Equal(3, result["c"]);
    }

    [Fact]
    public void EqualSplitIsZeroSumToExpenseAmount()
    {
        var amountMinor = 101L;
        var result = SplitEvaluator.EvaluateSplit(
            new Expense(
                "e1",
                "g1",
                "Test",
                "u1",
                amountMinor,
                "2026-01-01",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "u2", "u1" }, RemainderMode.Equal)
                })),
            new[]
            {
                new Participant("u1", "g1", "u1", "U1", ConsumptionCategory.Full),
                new Participant("u2", "g1", "u2", "U2", ConsumptionCategory.Full)
            });

        Assert.Equal(amountMinor, result.Values.Sum());
    }

    [Fact]
    public void CanonicalFixtureIsParsableAndDocumentsExpectedShares()
    {
        var json = File.ReadAllText(GetFixturePath());
        var fixture = JsonSerializer.Deserialize<FoundationFixture>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(fixture);
        Assert.Equal(10, fixture.AmountMinor);
        Assert.Equal(3, fixture.ExpectedShares.Count);
        Assert.Equal(4, fixture.ExpectedShares["a"]);
    }

    private static string GetFixturePath()
    {
        var currentFile = GetCurrentFilePath();
        var currentDir = Path.GetDirectoryName(currentFile)!;
        return Path.Combine(currentDir, "Fixtures", "foundation-parity.fixture.json");
    }

    private static string GetCurrentFilePath([CallerFilePath] string path = "") => path;

    private sealed record FoundationFixture(long AmountMinor, Dictionary<string, long> ExpectedShares);
}
