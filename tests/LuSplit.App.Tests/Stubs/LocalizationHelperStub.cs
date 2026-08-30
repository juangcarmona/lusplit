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
        new LanguageOption("it", "🇮🇹", () => "Italian"),
        new LanguageOption("pt", "🇵🇹", () => "Portuguese"),
        new LanguageOption("ar", "🇸🇦", () => "Arabic"),
        new LanguageOption("hi", "🇮🇳", () => "Hindi"),
        new LanguageOption("id", "🇮🇩", () => "Indonesian"),
        new LanguageOption("ja", "🇯🇵", () => "Japanese"),
        new LanguageOption("ko", "🇰🇷", () => "Korean"),
        new LanguageOption("ru", "🇷🇺", () => "Russian"),
        new LanguageOption("tr", "🇹🇷", () => "Turkish"),
        new LanguageOption("zh-CN", "🇨🇳", () => "Chinese (Simplified)"),
        new LanguageOption("zh-TW", "🇹🇼", () => "Chinese (Traditional)"),
    };

    public static string GetSavedLanguageCode() => string.Empty;

    public static void SetAndApplyLanguage(string cultureCode) { }
}

public sealed record LanguageOption(string Culture, string Flag, Func<string> NativeNameAccessor)
{
    public string DisplayLabel => $"{Flag} {NativeNameAccessor()}";
}
