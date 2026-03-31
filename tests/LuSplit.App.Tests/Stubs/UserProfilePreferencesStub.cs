namespace LuSplit.App.Services;

/// <summary>
/// Test-only stub for UserProfilePreferences.
/// Provides the API surface used by TripPresentationMapper without MAUI runtime dependencies.
/// </summary>
internal static class UserProfilePreferences
{
    public static string GetPreferredName() => string.Empty;
    public static void SetPreferredName(string? name) { }
    public static bool HasSeenPreferredNamePrompt() => false;
    public static void MarkPreferredNamePromptSeen() { }
    public static string AnnotateIfCurrentUser(string name) => name;
}
