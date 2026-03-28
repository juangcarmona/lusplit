using System.Globalization;

namespace LuSplit.App.Pages;

/// <summary>
/// Pure static helpers for parsing and normalising text input on the Add/Edit Expense pages.
/// Extracted from the private static helpers that were previously inlined in AddExpensePage.
/// </summary>
internal static class ExpenseAmountParser
{
    /// <summary>
    /// Returns true when <paramref name="input"/> is empty (user hasn't finished typing),
    /// a leading decimal point, or a valid non-negative decimal amount, in which case
    /// <paramref name="parsedMinor"/> is set to the minor-unit value.
    /// </summary>
    public static bool IsTransientInputAcceptable(string? input, out long? parsedMinor)
    {
        parsedMinor = null;
        var normalized = NormalizeNumberInput(input);

        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        if (normalized == ".")
            return true;

        var trailingDecimal = normalized.EndsWith(".", StringComparison.Ordinal);
        if (trailingDecimal)
        {
            normalized = normalized.TrimEnd('.');
            if (string.IsNullOrWhiteSpace(normalized))
                return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0)
                return false;

            parsedMinor = (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="input"/> is empty or a valid percentage between 0 and 100,
    /// in which case <paramref name="parsedPercentage"/> is set when the value is fully committed.
    /// </summary>
    public static bool IsTransientPercentageAcceptable(string? input, out decimal? parsedPercentage)
    {
        parsedPercentage = null;
        var normalized = NormalizeNumberInput(input);

        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        if (normalized == ".")
            return true;

        var trailingDecimal = normalized.EndsWith(".", StringComparison.Ordinal);
        if (trailingDecimal)
        {
            normalized = normalized.TrimEnd('.');
            if (string.IsNullOrWhiteSpace(normalized))
                return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0 || value > 100)
                return false;

            if (!trailingDecimal)
                parsedPercentage = value;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a total amount from a user-typed string. Returns true only when a positive value is produced.
    /// </summary>
    public static bool TryParseAmountLenient(string? text, out long amountMinor)
    {
        amountMinor = 0;
        var normalized = NormalizeNumberInput(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (normalized == "." || normalized.EndsWith(".", StringComparison.Ordinal))
            return false;

        if (decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            amountMinor = (long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
            return amountMinor > 0;
        }

        return false;
    }

    /// <summary>
    /// Strips currency symbols and normalises decimal separators so that the result can be passed
    /// to <see cref="decimal.TryParse(string, NumberStyles, IFormatProvider, out decimal)"/> with
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public static string NormalizeNumberInput(string? text)
    {
        var value = (text ?? string.Empty)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (value.Contains(',') && value.Contains('.'))
        {
            var lastComma = value.LastIndexOf(',');
            var lastDot = value.LastIndexOf('.');
            if (lastComma > lastDot)
                value = value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            else
                value = value.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (value.Contains(','))
        {
            value = value.Replace(',', '.');
        }

        return value;
    }
}
