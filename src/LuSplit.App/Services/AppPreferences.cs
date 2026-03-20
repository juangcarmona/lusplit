using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Services;

public static class AppPreferences
{
    private const string PreferredCurrencyKey = "user.profile.preferredCurrency";
    private const string DarkThemeEnabledKey = "user.profile.darkThemeEnabled";

    public static string GetPreferredCurrency()
        => Preferences.Default.Get(PreferredCurrencyKey, "USD").Trim().ToUpperInvariant();

    public static void SetPreferredCurrency(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        Preferences.Default.Set(PreferredCurrencyKey, normalized);
    }

    public static bool IsDarkThemeEnabled()
        => Preferences.Default.Get(DarkThemeEnabledKey, false);

    public static void SetDarkThemeEnabled(bool enabled)
    {
        Preferences.Default.Set(DarkThemeEnabledKey, enabled);
        MauiApplication.Current!.UserAppTheme = enabled ? AppTheme.Dark : AppTheme.Light;
    }
}
