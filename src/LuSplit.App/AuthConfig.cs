using System.Reflection;

namespace LuSplit.App;

internal static class AuthConfig
{
    public static string Authority =>
        Meta("LuSplitAuthority") ?? string.Empty;

    public static string MobileClientId =>
        Meta("LuSplitMobileClientId") ?? string.Empty;

    public static string RequiredScope =>
        Meta("LuSplitRequiredScope") ?? string.Empty;

    public static string FunctionsBaseUrl =>
        Meta("LuSplitFunctionsBaseUrl") ?? string.Empty;

    private static string? Meta(string key) =>
        typeof(AuthConfig).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == key)
            ?.Value;
}
