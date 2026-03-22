using System.Globalization;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Services;

public static class AppPreferences
{
    private const string PreferredCurrencyKey = "user.profile.preferredCurrency";
    private const string DarkThemeEnabledKey = "user.profile.darkThemeEnabled";

    public static string GetPreferredCurrency()
    {
        if (Preferences.Default.ContainsKey(PreferredCurrencyKey))
        {
            var saved = Preferences.Default.Get(PreferredCurrencyKey, CurrencyCatalog.DefaultCurrencyCode);
            var normalized = CurrencyCatalog.NormalizeSupportedOrDefault(saved);
            if (!string.Equals(saved, normalized, StringComparison.Ordinal))
            {
                Preferences.Default.Set(PreferredCurrencyKey, normalized);
            }

            return normalized;
        }

        var inferredCurrency = InferCurrencyFromDeviceLocale();
        Preferences.Default.Set(PreferredCurrencyKey, inferredCurrency);
        return inferredCurrency;
    }

    public static void InitializePreferredCurrencyIfNeeded()
    {
        _ = GetPreferredCurrency();
    }

    public static void SetPreferredCurrency(string? currency)
    {
        var normalized = CurrencyCatalog.NormalizeSupportedOrDefault(currency);
        Preferences.Default.Set(PreferredCurrencyKey, normalized);
    }

    public static bool IsDarkThemeEnabled()
        => Preferences.Default.Get(DarkThemeEnabledKey, false);

    public static void SetDarkThemeEnabled(bool enabled)
    {
        Preferences.Default.Set(DarkThemeEnabledKey, enabled);
        MauiApplication.Current!.UserAppTheme = enabled ? AppTheme.Dark : AppTheme.Light;
    }

    private static string InferCurrencyFromDeviceLocale()
    {
        try
        {
            var region = new RegionInfo(CultureInfo.CurrentCulture.Name);
            return CurrencyCatalog.NormalizeSupportedOrDefault(region.ISOCurrencySymbol, CurrencyCatalog.DefaultCurrencyCode);
        }
        catch
        {
            return CurrencyCatalog.DefaultCurrencyCode;
        }
    }
}
