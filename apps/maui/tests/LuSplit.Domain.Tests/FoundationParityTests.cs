using System.Runtime.CompilerServices;
using System.Text.Json;
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
        var result = DeterministicEqualSplit.Evaluate(new[] { "c", "a", "b" }, MoneyAmount.FromMinorUnits(10));

        Assert.Equal(4, result["a"]);
        Assert.Equal(3, result["b"]);
        Assert.Equal(3, result["c"]);
    }

    [Fact]
    public void EqualSplitIsZeroSumToExpenseAmount()
    {
        var amount = MoneyAmount.FromMinorUnits(101);
        var result = DeterministicEqualSplit.Evaluate(new[] { "u2", "u1" }, amount);

        Assert.Equal(amount.MinorUnits, result.Values.Sum());
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
