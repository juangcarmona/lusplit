using System.Globalization;

namespace LuSplit.App.Services.Formatting;

/// <summary>
/// Shared formatting utilities for amounts and currency symbols.
/// Centralises the FormatMinor helper that was previously duplicated in
/// TripPresentationMapper, ExpenseDetailsPage, and AddExpensePage.
/// </summary>
public static class CurrencyFormatter
{
    /// <summary>Returns the common symbol for well-known currency codes, or <see cref="string.Empty"/> for others.</summary>
    public static string GetSymbol(string currency)
        => currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => string.Empty
        };

    /// <summary>Formats a minor-unit amount (e.g. cents) as a display string for the given currency code.</summary>
    public static string FormatMinor(long minor, string currency)
    {
        var amount = minor / 100m;
        var symbol = GetSymbol(currency);

        return string.IsNullOrEmpty(symbol)
            ? string.Create(CultureInfo.CurrentCulture, $"{amount:0.00} {currency.ToUpperInvariant()}")
            : string.Create(CultureInfo.CurrentCulture, $"{symbol}{amount:0.00}");
    }
}
