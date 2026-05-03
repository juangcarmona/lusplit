namespace LuSplit.App.Services.Settings;

/// <summary>
/// Test stub for LinkedAccountStore. Returns empty defaults without MAUI Preferences dependency.
/// </summary>
internal static class LinkedAccountStore
{
    public static bool HasLinkedAccount => false;
    public static string? UserId => null;
    public static string? Username => null;
    public static string? DisplayName => null;
    public static void Save(string userId, string? username, string? displayName) { }
    public static void Clear() { }
}
