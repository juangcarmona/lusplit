using Microsoft.Maui.Storage;

namespace LuSplit.App.Services;

public static class UserProfilePreferences
{
    private const string PreferredNameKey = "user.profile.preferredName";

    public static string GetPreferredName()
        => Preferences.Default.Get(PreferredNameKey, string.Empty).Trim();

    public static void SetPreferredName(string? name)
        => Preferences.Default.Set(PreferredNameKey, (name ?? string.Empty).Trim());

    public static string AnnotateIfCurrentUser(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var preferredName = GetPreferredName();
        if (!string.IsNullOrWhiteSpace(preferredName)
            && string.Equals(name, preferredName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{name} ({LuSplit.App.Resources.Localization.AppResources.Mapper_Me})";
        }

        return name;
    }
}
