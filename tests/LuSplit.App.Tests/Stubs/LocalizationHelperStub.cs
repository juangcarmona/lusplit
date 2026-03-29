namespace LuSplit.App.Services;

/// <summary>
/// Test stub for LocalizationHelper. Returns stable values without MAUI runtime dependencies.
/// </summary>
internal static class LocalizationHelper
{
    public static string GetCapitalizedMeLabel() => "Me";

    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } = new[]
    {
        new LanguageOption("", "🌐", () => "SystemDefault"),
        new LanguageOption("en", "🇬🇧", () => "English"),
        new LanguageOption("es", "🇪🇸", () => "Spanish"),
        new LanguageOption("fr", "🇫🇷", () => "French"),
        new LanguageOption("de", "🇩🇪", () => "German"),
    };

    public static string GetSavedLanguageCode() => string.Empty;

    public static void SetAndApplyLanguage(string cultureCode) { }
}

public sealed record LanguageOption(string Culture, string Flag, Func<string> NativeNameAccessor)
{
    public string DisplayLabel => $"{Flag} {NativeNameAccessor()}";
}
