using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public sealed class ParticipantSplitRowViewModelTests
{
    // IsEditing / IsViewing derived flags

    [Fact]
    public void IsEditing_AutoMode_ReturnsFalse()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);

        Assert.False(row.IsEditing);
        Assert.True(row.IsViewing);
    }

    [Fact]
    public void IsEditing_FixedModeIncluded_ReturnsTrue()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            SplitMode = SplitMode.Fixed
        };

        Assert.True(row.IsEditing);
        Assert.False(row.IsViewing);
    }

    [Fact]
    public void IsEditing_FixedModeExcluded_ReturnsFalse()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", false)
        {
            SplitMode = SplitMode.Fixed
        };

        Assert.False(row.IsEditing);
    }

    [Fact]
    public void IsEditing_PercentageModeIncluded_ReturnsTrue()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            SplitMode = SplitMode.Percentage
        };

        Assert.True(row.IsEditing);
    }

    // ModeLabel

    [Fact]
    public void ModeLabel_AutoMode_ShowsAutoLabel()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);

        Assert.Equal("⚖️▼", row.ModeLabel);
    }

    [Fact]
    public void ModeLabel_FixedMode_ShowsFixedLabel()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            SplitMode = SplitMode.Fixed
        };

        Assert.Equal("💰▼", row.ModeLabel);
    }

    [Fact]
    public void ModeLabel_PercentageMode_ShowsPercentageLabel()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            SplitMode = SplitMode.Percentage
        };

        Assert.Equal("% ▼", row.ModeLabel);
    }

    // HasValidationError

    [Fact]
    public void HasValidationError_EmptyValidationError_ReturnsFalse()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);

        Assert.False(row.HasValidationError);
    }

    [Fact]
    public void HasValidationError_NonEmptyValidationError_ReturnsTrue()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            ValidationError = "Invalid amount"
        };

        Assert.True(row.HasValidationError);
    }

    // HasGroupHeader

    [Fact]
    public void HasGroupHeader_NullGroupHeader_ReturnsFalse()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);

        Assert.False(row.HasGroupHeader);
    }

    [Fact]
    public void HasGroupHeader_NonEmptyGroupHeader_ReturnsTrue()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            GroupHeader = "Adults"
        };

        Assert.True(row.HasGroupHeader);
    }

    // CommittedAmountMinor clamps to zero

    [Fact]
    public void CommittedAmountMinor_NegativeValue_ClampsToZero()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true)
        {
            CommittedAmountMinor = -100
        };

        Assert.Equal(0L, row.CommittedAmountMinor);
    }

    // PropertyChanged notifications

    [Fact]
    public void IsIncluded_Change_RaisesPropertyChangedForDerivedFlags()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsIncluded = false;

        Assert.Contains(nameof(row.IsIncluded), raised);
        Assert.Contains(nameof(row.IsEditing), raised);
        Assert.Contains(nameof(row.IsViewing), raised);
    }

    [Fact]
    public void SplitMode_Change_RaisesPropertyChangedForModeLabel()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.SplitMode = SplitMode.Fixed;

        Assert.Contains(nameof(row.ModeLabel), raised);
    }

    [Fact]
    public void ValidationError_Change_RaisesPropertyChangedForHasValidationError()
    {
        var row = new ParticipantSplitRowViewModel("p1", "Alice", true);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.ValidationError = "bad";

        Assert.Contains(nameof(row.HasValidationError), raised);
    }
}
