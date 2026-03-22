using System.Globalization;
using LuSplit.App.Resources.Localization;
using MauiApplication = Microsoft.Maui.Controls.Application;
using MauiMainThread = Microsoft.Maui.ApplicationModel.MainThread;

namespace LuSplit.App.Services;

/// <summary>
/// Handles language detection, persistence, and application for the app.
/// </summary>
public static class LocalizationHelper
{
    private const string LanguagePreferenceKey = "app_language";
    private const string DefaultLanguageCode = "en";

    /// <summary>
    /// The cultures explicitly supported by this app (satellite RESX files exist for these).
    /// </summary>
    public static readonly IReadOnlyList<LanguageOption> SupportedLanguages = new[]
    {
        new LanguageOption("", "🌐", () => AppResources.Language_SystemDefault),
        new LanguageOption("en", "🇬🇧", () => AppResources.Language_English),
        new LanguageOption("es", "🇪🇸", () => AppResources.Language_Spanish),
        new LanguageOption("fr", "🇫🇷", () => AppResources.Language_French),
        new LanguageOption("de", "🇩🇪", () => AppResources.Language_German),
        new LanguageOption("it", "🇮🇹", () => AppResources.Language_Italian),
        new LanguageOption("pt", "🇵🇹", () => AppResources.Language_Portuguese),
    };

    private static readonly HashSet<string> _supportedCodes =
        new(SupportedLanguages.Select(l => l.Culture).Where(c => c.Length > 0), StringComparer.OrdinalIgnoreCase);

    // Captured once at process start so "System Default" can truly restore the original OS culture.
    private static readonly CultureInfo _osCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Reads persisted language preference and applies it. Called once during app startup.
    /// </summary>
    public static void ApplyPersistedLanguage()
    {
        InitializePreferredLanguageIfNeeded();

        var saved = GetSavedLanguageCode();

        if (string.IsNullOrEmpty(saved))
        {
            ApplyCulture(_osCulture);
            return;
        }

        if (_supportedCodes.Contains(saved))
        {
            ApplyCulture(new CultureInfo(saved));
            return;
        }

        ApplyCulture(new CultureInfo(DefaultLanguageCode));
    }

    /// <summary>Returns the language code saved in preferences, or empty string for System Default.</summary>
    public static string GetSavedLanguageCode() =>
        Preferences.Default.Get(LanguagePreferenceKey, string.Empty);

    /// <summary>
    /// Initializes the persisted language preference one time on first app startup.
    /// If the preference already exists, user-selected value is preserved.
    /// </summary>
    public static void InitializePreferredLanguageIfNeeded()
    {
        if (Preferences.Default.ContainsKey(LanguagePreferenceKey))
        {
            return;
        }

        var inferred = InferSupportedLanguageFromSystem();
        Preferences.Default.Set(LanguagePreferenceKey, inferred);
    }

    /// <summary>
    /// Persists the chosen language code, applies the culture, and rebuilds the UI.
    /// Pass an empty string to restore System Default.
    /// </summary>
    public static void SetAndApplyLanguage(string cultureCode)
    {
        Preferences.Default.Set(LanguagePreferenceKey, cultureCode ?? string.Empty);

        if (string.IsNullOrEmpty(cultureCode))
        {
            ApplyCulture(_osCulture);
        }
        else
        {
            ApplyCulture(new CultureInfo(cultureCode));
        }

        RebuildUi();
    }

    // -----------------------------------------------------------------------

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private static void RebuildUi()
    {
        MauiMainThread.BeginInvokeOnMainThread(() =>
        {
            var services = App.Services;
            if (services is null) return;

            var window = MauiApplication.Current?.Windows.FirstOrDefault();
            if (window is null) return;

            window.Page = services.GetRequiredService<AppShell>();
        });
    }

    /// <summary>
    /// Infers a supported app language from system UI culture.
    /// Candidate priority: full culture (e.g. es-ES), then two-letter language code (es),
    /// then installed UI culture two-letter code. When a full culture is matched in supported codes,
    /// it is normalized to its two-letter language component before persistence.
    /// Falls back to default app language when unsupported.
    /// </summary>
    public static string InferSupportedLanguageFromSystem()
    {
        var culture = CultureInfo.CurrentUICulture;
        var candidates = new[]
        {
            culture.Name,
            culture.TwoLetterISOLanguageName,
            CultureInfo.InstalledUICulture.TwoLetterISOLanguageName
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Trim();
            if (_supportedCodes.Contains(normalized))
            {
                return normalized.Length > 2 ? normalized[..2] : normalized;
            }
        }

        return DefaultLanguageCode;
    }

    /// <summary>
    /// Returns the localized "me" label with a leading uppercase character for use as a fallback display name.
    /// </summary>
    public static string GetCapitalizedMeLabel()
    {
        var localizedMe = AppResources.Mapper_Me;
        if (string.IsNullOrWhiteSpace(localizedMe))
        {
            return AppResources.Common_MeCapitalized;
        }

        return localizedMe.Length == 1
            ? char.ToUpperInvariant(localizedMe[0]).ToString()
            : char.ToUpperInvariant(localizedMe[0]) + localizedMe[1..];
    }
}

/// <summary>A language available in the language picker.</summary>
public sealed record LanguageOption(string Culture, string Flag, Func<string> NativeNameAccessor)
{
    public string DisplayLabel => $"{Flag} {NativeNameAccessor()}";
}
