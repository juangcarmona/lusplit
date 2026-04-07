namespace LuSplit.App.Services.Settings;

/// <summary>
/// Thin wrapper over MAUI AppInfo so ViewModels can read installed package metadata
/// without taking a direct runtime dependency on AppInfo.Current.
/// Test projects substitute this via a stub in the LuSplit.App.Services namespace.
/// </summary>
public static class AppInfoProvider
{
    /// <summary>
    /// The display version injected at publish time via ApplicationDisplayVersion
    /// (e.g. "1.0.18"). Returns an empty string if no runtime metadata is available.
    /// </summary>
    public static string VersionString => AppInfo.Current.VersionString;

    /// <summary>
    /// The build number injected at publish time via ApplicationVersion (e.g. "18").
    /// Returns an empty string if no runtime metadata is available.
    /// </summary>
    public static string BuildString => AppInfo.Current.BuildString;
}
