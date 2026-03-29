using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public sealed class AddExpenseSplitCalculationsTests
{
    private const string InvalidMsg = "invalid";
    private const string AtLeastOneMsg = "at_least_one";

    private static ParticipantSplitRowViewModel MakeRow(string id, bool included = true)
        => new(id, id, included);

    // Auto-split equal distribution

    [Fact]
    public void ComputeRows_TwoAutoRows_SplitsEquallyInMinorUnits()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };

        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(string.Empty, result);
        Assert.Equal(500L, rows[0].CommittedAmountMinor);
        Assert.Equal(500L, rows[1].CommittedAmountMinor);
    }

    [Fact]
    public void ComputeRows_ThreeAutoRows_DistributesRemainderToEarlierRows()
    {
        // $1.00 → 100 minor, 3 rows: 34 + 33 + 33
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2"),
            MakeRow("p3")
        };

        var result = AddExpenseSplitCalculations.ComputeRows(rows, "1.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(string.Empty, result);
        Assert.Equal(34L, rows[0].CommittedAmountMinor);
        Assert.Equal(33L, rows[1].CommittedAmountMinor);
        Assert.Equal(33L, rows[2].CommittedAmountMinor);
    }

    // Fewer than 2 included participants

    [Fact]
    public void ComputeRows_OnlyOneIncluded_ReturnsAtLeastOneMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1", included: true),
            MakeRow("p2", included: false)
        };

        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(AtLeastOneMsg, result);
    }

    [Fact]
    public void ComputeRows_ZeroIncluded_ReturnsAtLeastOneMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1", included: false)
        };

        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(AtLeastOneMsg, result);
    }

    // Invalid amount

    [Fact]
    public void ComputeRows_EmptyAmount_ReturnsInvalidAmountMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };

        var result = AddExpenseSplitCalculations.ComputeRows(rows, string.Empty, InvalidMsg, AtLeastOneMsg);

        Assert.Equal(InvalidMsg, result);
    }

    [Fact]
    public void ComputeRows_EmptyAmount_ResetsAllShares()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].CommittedAmountMinor = 500;
        rows[1].CommittedAmountMinor = 500;

        AddExpenseSplitCalculations.ComputeRows(rows, string.Empty, InvalidMsg, AtLeastOneMsg);

        Assert.Equal(0L, rows[0].CommittedAmountMinor);
        Assert.Equal(0L, rows[1].CommittedAmountMinor);
    }

    // Fixed split that exceeds the total

    [Fact]
    public void ComputeRows_FixedSumExceedsTotal_ReturnsInvalidAmountMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].SplitMode = SplitMode.Fixed;
        rows[0].RawInput = "8.00";
        rows[0].CommittedAmountMinor = 800;

        rows[1].SplitMode = SplitMode.Fixed;
        rows[1].RawInput = "5.00";
        rows[1].CommittedAmountMinor = 500;

        // 8 + 5 = 13, but total is 10
        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(InvalidMsg, result);
    }

    // Percentage split

    [Fact]
    public void ComputeRows_PercentageSplit_AssignsCorrectMinorUnits()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].SplitMode = SplitMode.Percentage;
        rows[0].RawInput = "30";
        rows[0].CommittedPercentage = 30m;

        // p2 is auto; gets remaining 70%
        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(string.Empty, result);
        Assert.Equal(300L, rows[0].CommittedAmountMinor);   // 30% of 1000 minor
        Assert.Equal(700L, rows[1].CommittedAmountMinor);   // remainder
    }

    [Fact]
    public void ComputeRows_PercentageSumExceedsTotal_ReturnsInvalidAmountMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].SplitMode = SplitMode.Percentage;
        rows[0].RawInput = "70";
        rows[0].CommittedPercentage = 70m;

        rows[1].SplitMode = SplitMode.Percentage;
        rows[1].RawInput = "60";
        rows[1].CommittedPercentage = 60m;

        // 70% + 60% = 130% of total, remaining < 0
        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(InvalidMsg, result);
    }

    // Transient invalid input guard

    [Fact]
    public void ComputeRows_RowWithTransientInvalidInput_ReturnsInvalidAmountMessage()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].HasTransientInvalidInput = true;

        var result = AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(InvalidMsg, result);
    }

    // Excluded rows have their shares zeroed

    [Fact]
    public void ComputeRows_ExcludedRow_HasShareResetToZero()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1", included: true),
            MakeRow("p2", included: true),
            MakeRow("p3", included: false)
        };
        rows[2].CommittedAmountMinor = 999;

        AddExpenseSplitCalculations.ComputeRows(rows, "10.00", InvalidMsg, AtLeastOneMsg);

        Assert.Equal(0L, rows[2].CommittedAmountMinor);
    }

    // ResetAllShares

    [Fact]
    public void ResetAllShares_ClearsAllCommittedAmounts()
    {
        var rows = new List<ParticipantSplitRowViewModel>
        {
            MakeRow("p1"),
            MakeRow("p2")
        };
        rows[0].CommittedAmountMinor = 300;
        rows[1].CommittedAmountMinor = 700;

        AddExpenseSplitCalculations.ResetAllShares(rows);

        Assert.All(rows, r => Assert.Equal(0L, r.CommittedAmountMinor));
    }
}
