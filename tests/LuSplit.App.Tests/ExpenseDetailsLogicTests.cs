using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public sealed class ExpenseDetailsLogicTests
{
    // EvaluateSaveState

    [Fact]
    public void EvaluateSaveState_NotInEditMode_ReturnsFalse()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 1000) };

        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 1000, isEditMode: false, "Alice", "Dinner");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_NoIncludedRows_ReturnsFalse()
    {
        var row = Row("p1", "Alice", isPayer: false, 1000);
        row.IsIncluded = false;
        var rows = new[] { row };

        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 0, isEditMode: true, "Bob", "Dinner");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_EmptyTitle_ReturnsFalse()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 1000) };

        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 1000, isEditMode: true, "Bob", expenseTitle: "");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_NullPayer_ReturnsFalse()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 1000) };

        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 1000, isEditMode: true, selectedPayerName: null, "Dinner");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_TotalDoesNotMatchFixed_ReturnsFalse()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 500) };

        // fixedTotalMinor is 1000 but row total is 500
        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 1000, isEditMode: true, "Bob", "Dinner");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_ZeroRowTotal_ReturnsFalse()
    {
        var row = Row("p1", "Alice", isPayer: false, 0);
        var rows = new[] { row };

        // fixedTotalMinor is also 0 — matches, but totalMinor > 0 fails
        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 0, isEditMode: true, "Bob", "Dinner");

        Assert.False(result);
    }

    [Fact]
    public void EvaluateSaveState_AllConditionsMet_ReturnsTrue()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 1000) };

        var result = ExpenseDetailsLogic.EvaluateSaveState(rows, 1000, isEditMode: true, "Bob", "Dinner");

        Assert.True(result);
    }

    [Fact]
    public void EvaluateSaveState_OnlyIncludedRowsContributeToTotal()
    {
        var alice = Row("p1", "Alice", isPayer: false, 600);
        var carol = Row("p2", "Carol", isPayer: false, 400);
        carol.IsIncluded = false;   // carol excluded → total counted is 600 not 1000

        var result = ExpenseDetailsLogic.EvaluateSaveState(
            new[] { alice, carol }, 1000, isEditMode: true, "Bob", "Dinner");

        Assert.False(result);   // 600 ≠ 1000
    }

    // TotalMatchesFixed

    [Fact]
    public void TotalMatchesFixed_ExactMatch_ReturnsTrue()
    {
        var rows = new[]
        {
            Row("p1", "Alice", isPayer: false, 600),
            Row("p2", "Carol", isPayer: false, 400)
        };

        Assert.True(ExpenseDetailsLogic.TotalMatchesFixed(rows, 1000));
    }

    [Fact]
    public void TotalMatchesFixed_Mismatch_ReturnsFalse()
    {
        var rows = new[] { Row("p1", "Alice", isPayer: false, 999) };

        Assert.False(ExpenseDetailsLogic.TotalMatchesFixed(rows, 1000));
    }

    [Fact]
    public void TotalMatchesFixed_ExcludedRowsNotCounted()
    {
        var alice = Row("p1", "Alice", isPayer: false, 600);
        var carol = Row("p2", "Carol", isPayer: false, 400);
        carol.IsIncluded = false;

        Assert.False(ExpenseDetailsLogic.TotalMatchesFixed(new[] { alice, carol }, 1000));
    }

    // BuildPreviewLines

    [Fact]
    public void BuildPreviewLines_ExcludesPayer()
    {
        var payer = Row("p1", "Bob", isPayer: true, 1000);
        var alice = Row("p2", "Alice", isPayer: false, 500);
        var rows = new[] { payer, alice };

        var lines = ExpenseDetailsLogic.BuildPreviewLines(rows, "Bob", "USD");

        Assert.Single(lines);
        Assert.Contains("Alice", lines[0]);
    }

    [Fact]
    public void BuildPreviewLines_ExcludesNonIncludedRows()
    {
        var alice = Row("p1", "Alice", isPayer: false, 500);
        alice.IsIncluded = false;
        var rows = new[] { alice };

        var lines = ExpenseDetailsLogic.BuildPreviewLines(rows, "Bob", "USD");

        Assert.Empty(lines);
    }

    [Fact]
    public void BuildPreviewLines_ExcludesZeroAmountRows()
    {
        var alice = Row("p1", "Alice", isPayer: false, 0);
        var rows = new[] { alice };

        var lines = ExpenseDetailsLogic.BuildPreviewLines(rows, "Bob", "USD");

        Assert.Empty(lines);
    }

    [Fact]
    public void BuildPreviewLines_LineContainsNameAndPayer()
    {
        var alice = Row("p1", "Alice", isPayer: false, 500);
        var rows = new[] { alice };

        var lines = ExpenseDetailsLogic.BuildPreviewLines(rows, "Bob", "USD");

        Assert.Single(lines);
        Assert.Contains("Alice", lines[0]);
        Assert.Contains("Bob", lines[0]);
    }

    [Fact]
    public void BuildPreviewLines_EmptyRows_ReturnsEmpty()
    {
        var lines = ExpenseDetailsLogic.BuildPreviewLines(Array.Empty<ExpenseParticipantRowViewModel>(), "Bob", "USD");

        Assert.Empty(lines);
    }

    private static ExpenseParticipantRowViewModel Row(string id, string name, bool isPayer, long amountMinor) =>
        new(id, name, isPayer, amountMinor, "USD");
}
