using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Services.Formatting;

public static class CurrencyCatalog
{
    private static readonly string[] SupportedCodes =
    {
        "EUR", "USD", "GBP", "CHF", "JPY", "CNY", "INR", "AUD", "CAD", "BRL",
        "MXN", "ARS", "CLP", "COP", "PEN", "SEK", "NOK", "DKK", "PLN", "CZK",
        "HUF", "RON", "TRY", "AED", "SAR", "KRW", "SGD", "HKD", "NZD", "ZAR"
    };

    private static readonly HashSet<string> SupportedCodeSet = new(SupportedCodes, StringComparer.OrdinalIgnoreCase);

    public static string DefaultCurrencyCode => "USD";

    /// <summary>Returns true when the provided code is part of the supported currency catalog.</summary>
    public static bool IsSupported(string? code)
        => !string.IsNullOrWhiteSpace(code) && SupportedCodeSet.Contains(code.Trim());

    /// <summary>
    /// Normalizes a currency code and guarantees a supported value by falling back to the provided fallback code
    /// (or the catalog default when fallback is not provided).
    /// </summary>
    public static string NormalizeSupportedOrDefault(string? code, string? fallbackCode = null)
    {
        var normalizedFallback = string.IsNullOrWhiteSpace(fallbackCode)
            ? DefaultCurrencyCode
            : fallbackCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
        {
            return normalizedFallback;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        return IsSupported(normalizedCode) ? normalizedCode : normalizedFallback;
    }

    /// <summary>Returns all supported currencies in a stable, predefined order.</summary>
    public static IReadOnlyList<CurrencyOption> GetSupportedCurrencyOptions()
        => SupportedCodes
            .Select(BuildOption)
            .ToArray();

    /// <summary>
    /// Returns a currency option for any input code, preserving unknown code values while still generating a label.
    /// </summary>
    public static CurrencyOption GetOrCreateOption(string? code)
    {
        var normalized = string.IsNullOrWhiteSpace(code)
            ? DefaultCurrencyCode
            : code.Trim().ToUpperInvariant();
        return BuildOption(normalized);
    }

    /// <summary>Finds a currency option by code from an existing options sequence.</summary>
    public static CurrencyOption? FindByCode(IEnumerable<CurrencyOption> options, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();
        return options.FirstOrDefault(option => string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears <paramref name="target"/> and fills it with the full list of supported currency options
    /// in the catalog's canonical order.
    /// </summary>
    public static void PopulateSupportedOptions(System.Collections.ObjectModel.ObservableCollection<CurrencyOption> target)
    {
        target.Clear();
        foreach (var option in GetSupportedCurrencyOptions())
        {
            target.Add(option);
        }
    }

    private static CurrencyOption BuildOption(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var symbol = GetSymbol(normalized);
        var localizedName = GetLocalizedName(normalized);
        var displayLabel = string.Format(AppResources.Currency_DisplayFormat, symbol, localizedName, normalized);
        return new CurrencyOption(normalized, displayLabel);
    }

    private static string GetSymbol(string code)
        => code switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            "CHF" => "CHF",
            "JPY" => "¥",
            "CNY" => "¥",
            "INR" => "₹",
            "AUD" => "A$",
            "CAD" => "C$",
            "BRL" => "R$",
            "MXN" => "MX$",
            "ARS" => "AR$",
            "CLP" => "CLP$",
            "COP" => "COP$",
            "PEN" => "S/",
            "SEK" => "kr",
            "NOK" => "kr",
            "DKK" => "kr",
            "PLN" => "zł",
            "CZK" => "Kč",
            "HUF" => "Ft",
            "RON" => "lei",
            "TRY" => "₺",
            "AED" => "د.إ",
            "SAR" => "﷼",
            "KRW" => "₩",
            "SGD" => "S$",
            "HKD" => "HK$",
            "NZD" => "NZ$",
            "ZAR" => "R",
            _ => code
        };

    // Note: CurrencyFormatter.GetSymbol covers the three common symbols (USD/EUR/GBP)
    // used in formatting. The full symbol table above is retained here for the
    // broader display label used in the currency picker (BuildOption).

    private static string GetLocalizedName(string code)
        => code switch
        {
            "EUR" => AppResources.Currency_Name_EUR,
            "USD" => AppResources.Currency_Name_USD,
            "GBP" => AppResources.Currency_Name_GBP,
            "CHF" => AppResources.Currency_Name_CHF,
            "JPY" => AppResources.Currency_Name_JPY,
            "CNY" => AppResources.Currency_Name_CNY,
            "INR" => AppResources.Currency_Name_INR,
            "AUD" => AppResources.Currency_Name_AUD,
            "CAD" => AppResources.Currency_Name_CAD,
            "BRL" => AppResources.Currency_Name_BRL,
            "MXN" => AppResources.Currency_Name_MXN,
            "ARS" => AppResources.Currency_Name_ARS,
            "CLP" => AppResources.Currency_Name_CLP,
            "COP" => AppResources.Currency_Name_COP,
            "PEN" => AppResources.Currency_Name_PEN,
            "SEK" => AppResources.Currency_Name_SEK,
            "NOK" => AppResources.Currency_Name_NOK,
            "DKK" => AppResources.Currency_Name_DKK,
            "PLN" => AppResources.Currency_Name_PLN,
            "CZK" => AppResources.Currency_Name_CZK,
            "HUF" => AppResources.Currency_Name_HUF,
            "RON" => AppResources.Currency_Name_RON,
            "TRY" => AppResources.Currency_Name_TRY,
            "AED" => AppResources.Currency_Name_AED,
            "SAR" => AppResources.Currency_Name_SAR,
            "KRW" => AppResources.Currency_Name_KRW,
            "SGD" => AppResources.Currency_Name_SGD,
            "HKD" => AppResources.Currency_Name_HKD,
            "NZD" => AppResources.Currency_Name_NZD,
            "ZAR" => AppResources.Currency_Name_ZAR,
            _ => AppResources.Currency_Name_Unknown
        };
}

public sealed record CurrencyOption(string Code, string DisplayLabel);
