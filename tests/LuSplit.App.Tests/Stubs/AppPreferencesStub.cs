namespace LuSplit.App.Services;

/// <summary>
/// Test stub for AppPreferences. Returns stable defaults without MAUI runtime dependencies.
/// </summary>
internal static class AppPreferences
{
    public static string GetPreferredCurrency() => "USD";
    public static void SetPreferredCurrency(string? currency) { }
    public static bool IsDarkThemeEnabled() => false;
    public static void SetDarkThemeEnabled(bool enabled) { }
}
