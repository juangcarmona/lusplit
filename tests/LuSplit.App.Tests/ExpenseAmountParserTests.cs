using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public sealed class ExpenseAmountParserTests
{
    // TryParseAmountLenient

    [Fact]
    public void TryParseAmountLenient_ValidWholeNumber_ReturnsMinorUnits()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("100", out var minor);

        Assert.True(ok);
        Assert.Equal(10000L, minor);
    }

    [Fact]
    public void TryParseAmountLenient_DecimalValue_ReturnsRoundedMinorUnits()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("12.50", out var minor);

        Assert.True(ok);
        Assert.Equal(1250L, minor);
    }

    [Fact]
    public void TryParseAmountLenient_CommaSeparator_IsTreatedAsDecimal()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("9,99", out var minor);

        Assert.True(ok);
        Assert.Equal(999L, minor);
    }

    [Fact]
    public void TryParseAmountLenient_EmptyString_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient(string.Empty, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseAmountLenient_Null_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient(null, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseAmountLenient_Zero_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("0", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseAmountLenient_TrailingDecimalPoint_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("10.", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseAmountLenient_CurrencySymbolPrefix_StripsAndParses()
    {
        var ok = ExpenseAmountParser.TryParseAmountLenient("$50", out var minor);

        Assert.True(ok);
        Assert.Equal(5000L, minor);
    }

    // IsTransientInputAcceptable

    [Fact]
    public void IsTransientInputAcceptable_Empty_ReturnsTrueWithNullParsed()
    {
        var ok = ExpenseAmountParser.IsTransientInputAcceptable(string.Empty, out var parsed);

        Assert.True(ok);
        Assert.Null(parsed);
    }

    [Fact]
    public void IsTransientInputAcceptable_LeadingDecimalPoint_ReturnsTrue()
    {
        var ok = ExpenseAmountParser.IsTransientInputAcceptable(".", out var parsed);

        Assert.True(ok);
        Assert.Null(parsed);
    }

    [Fact]
    public void IsTransientInputAcceptable_TrailingDecimalPoint_ReturnsTrueWithCommittedIntegerPart()
    {
        // "10." is acceptable (user mid-typing); the stripped integer part 10 is committed
        var ok = ExpenseAmountParser.IsTransientInputAcceptable("10.", out var parsed);

        Assert.True(ok);
        Assert.Equal(1000L, parsed);
    }

    [Fact]
    public void IsTransientInputAcceptable_ValidAmount_ReturnsTrueWithMinorUnits()
    {
        var ok = ExpenseAmountParser.IsTransientInputAcceptable("25.50", out var parsed);

        Assert.True(ok);
        Assert.Equal(2550L, parsed);
    }

    [Fact]
    public void IsTransientInputAcceptable_NegativeValue_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.IsTransientInputAcceptable("-5", out _);

        Assert.False(ok);
    }

    // IsTransientPercentageAcceptable

    [Fact]
    public void IsTransientPercentageAcceptable_ValidPercentage_ReturnsTrueWithValue()
    {
        var ok = ExpenseAmountParser.IsTransientPercentageAcceptable("33.5", out var parsed);

        Assert.True(ok);
        Assert.Equal(33.5m, parsed);
    }

    [Fact]
    public void IsTransientPercentageAcceptable_OneHundred_ReturnsTrue()
    {
        var ok = ExpenseAmountParser.IsTransientPercentageAcceptable("100", out var parsed);

        Assert.True(ok);
        Assert.Equal(100m, parsed);
    }

    [Fact]
    public void IsTransientPercentageAcceptable_OverOneHundred_ReturnsFalse()
    {
        var ok = ExpenseAmountParser.IsTransientPercentageAcceptable("100.1", out _);

        Assert.False(ok);
    }

    [Fact]
    public void IsTransientPercentageAcceptable_TrailingDecimalPoint_ReturnsTrueWithNullParsed()
    {
        var ok = ExpenseAmountParser.IsTransientPercentageAcceptable("50.", out var parsed);

        Assert.True(ok);
        Assert.Null(parsed);
    }

    // NormalizeNumberInput

    [Fact]
    public void NormalizeNumberInput_CommaOnly_ConvertsToDot()
    {
        var result = ExpenseAmountParser.NormalizeNumberInput("1,5");

        Assert.Equal("1.5", result);
    }

    [Fact]
    public void NormalizeNumberInput_ThousandsSeparatorWithDot_RemovesCommas()
    {
        // "1,234.56" → comma is thousands separator
        var result = ExpenseAmountParser.NormalizeNumberInput("1,234.56");

        Assert.Equal("1234.56", result);
    }

    [Fact]
    public void NormalizeNumberInput_EuroSymbol_IsStripped()
    {
        var result = ExpenseAmountParser.NormalizeNumberInput("€12");

        Assert.Equal("12", result);
    }
}
