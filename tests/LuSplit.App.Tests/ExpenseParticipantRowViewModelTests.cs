using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public sealed class ExpenseParticipantRowViewModelTests
{
    // IsIncluded

    [Fact]
    public void IsIncluded_SetFalse_ClampsAmountMinorToZero()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 500, "USD");
        row.IsIncluded = false;

        Assert.Equal(0L, row.AmountMinor);
    }

    [Fact]
    public void IsIncluded_Change_RaisesSelectMarkNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsIncluded = false;

        Assert.Contains(nameof(row.SelectMark), raised);
    }

    [Fact]
    public void IsIncluded_Change_RaisesCanEditAmountNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsIncluded = false;

        Assert.Contains(nameof(row.CanEditAmount), raised);
    }

    [Fact]
    public void SelectMark_WhenIncluded_IsCheckMark()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD") { IsIncluded = true };

        Assert.Equal("✓", row.SelectMark);
    }

    [Fact]
    public void SelectMark_WhenExcluded_IsSpace()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD") { IsIncluded = false };

        Assert.Equal(" ", row.SelectMark);
    }

    // IsPayer

    [Fact]
    public void IsPayer_SetTrue_NameAndPayerIncludesLabel()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");

        row.IsPayer = true;

        Assert.Contains("(payer)", row.NameAndPayer);
    }

    [Fact]
    public void IsPayer_SetFalse_NameAndPayerIsJustName()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", true, 0, "USD");

        row.IsPayer = false;

        Assert.Equal("Alice", row.NameAndPayer);
    }

    [Fact]
    public void IsPayer_Change_RaisesCanEditAmountNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        row.IsEditMode = true;
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsPayer = true;

        Assert.Contains(nameof(row.CanEditAmount), raised);
    }

    // IsEditMode

    [Fact]
    public void IsEditMode_Change_RaisesCanEditAmountNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsEditMode = true;

        Assert.Contains(nameof(row.CanEditAmount), raised);
    }

    [Fact]
    public void IsEditMode_Change_RaisesIsViewingNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsEditMode = true;

        Assert.Contains(nameof(row.IsViewing), raised);
    }

    // CanEditAmount

    [Fact]
    public void CanEditAmount_WhenEditModeIncludedNotPayer_IsTrue()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            IsEditMode = true,
            IsIncluded = true
        };

        Assert.True(row.CanEditAmount);
    }

    [Fact]
    public void CanEditAmount_WhenPayer_IsFalse()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", true, 0, "USD")
        {
            IsEditMode = true,
            IsIncluded = true
        };

        Assert.False(row.CanEditAmount);
    }

    [Fact]
    public void CanEditAmount_WhenNotInEditMode_IsFalse()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            IsEditMode = false,
            IsIncluded = true
        };

        Assert.False(row.CanEditAmount);
    }

    [Fact]
    public void CanEditAmount_WhenExcluded_IsFalse()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            IsEditMode = true,
            IsIncluded = false
        };

        Assert.False(row.CanEditAmount);
    }

    // IsEditing / IsViewing

    [Fact]
    public void IsEditing_SetTrue_IsViewingIsFalse()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            IsEditing = true
        };

        Assert.False(row.IsViewing);
    }

    [Fact]
    public void IsEditing_SetFalse_IsViewingIsTrue()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            IsEditing = false
        };

        Assert.True(row.IsViewing);
    }

    [Fact]
    public void IsEditing_Change_RaisesIsViewingNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.IsEditing = true;

        Assert.Contains(nameof(row.IsViewing), raised);
    }

    // AmountMinor

    [Fact]
    public void AmountMinor_NegativeValue_ClampsToZero()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD")
        {
            AmountMinor = -100
        };

        Assert.Equal(0L, row.AmountMinor);
    }

    [Fact]
    public void AmountMinor_Change_RaisesAmountTextNotification()
    {
        var row = new ExpenseParticipantRowViewModel("p1", "Alice", false, 0, "USD");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.AmountMinor = 500;

        Assert.Contains(nameof(row.AmountText), raised);
    }
}
