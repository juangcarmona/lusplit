using CommunityToolkit.Mvvm.ComponentModel;

namespace LuSplit.App.Services;

/// <summary>
/// Test stub for SessionService. Provides the minimal surface needed to compile
/// SettingsViewModel without a MAUI runtime. App.Services is null in tests, so
/// this type is never actually instantiated.
/// </summary>
internal sealed class SessionService : ObservableObject
{
    public bool IsSignedIn { get; }
    public string? Username { get; }
    public string? DisplayName { get; }
    public string? UserId { get; }
}
