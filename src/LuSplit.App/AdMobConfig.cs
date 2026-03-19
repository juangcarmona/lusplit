using System.Reflection;

namespace LuSplit.App;

public static class AdMobConfig
{
    public static string BannerId =>
        typeof(AdMobConfig).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "AdMobBannerId")
            ?.Value
        ?? string.Empty;
}