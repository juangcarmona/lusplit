using Microsoft.Maui.Storage;

namespace LuSplit.App.Services.Settings;

/// <summary>
/// Persists linked-account metadata locally via Preferences so the UI can
/// render signed-in state instantly, survive page navigation and app restarts,
/// and clear cleanly on sign-out. Does not store secrets — MSAL manages its
/// own token cache separately.
/// </summary>
public static class LinkedAccountStore
{
    private const string UserIdKey = "account.userId";
    private const string UsernameKey = "account.username";
    private const string DisplayNameKey = "account.displayName";

    public static bool HasLinkedAccount =>
        !string.IsNullOrEmpty(Preferences.Default.Get(UserIdKey, string.Empty));

    public static string? UserId =>
        NullIfEmpty(Preferences.Default.Get(UserIdKey, string.Empty));

    public static string? Username =>
        NullIfEmpty(Preferences.Default.Get(UsernameKey, string.Empty));

    public static string? DisplayName =>
        NullIfEmpty(Preferences.Default.Get(DisplayNameKey, string.Empty));

    public static void Save(string userId, string? username, string? displayName)
    {
        Preferences.Default.Set(UserIdKey, userId);
        Preferences.Default.Set(UsernameKey, username ?? string.Empty);
        Preferences.Default.Set(DisplayNameKey, displayName ?? string.Empty);
    }

    public static void Clear()
    {
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(UsernameKey);
        Preferences.Default.Remove(DisplayNameKey);
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
