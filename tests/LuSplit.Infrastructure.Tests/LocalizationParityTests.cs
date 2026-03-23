using System.Xml.Linq;

namespace LuSplit.Infrastructure.Tests;

public sealed class LocalizationParityTests
{
    private static readonly string LocalizationDir = ResolveLocalizationDir();

    private static string ResolveLocalizationDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LuSplit.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Could not locate solution root (LuSplit.slnx).");

        return Path.Combine(dir.FullName, "src", "LuSplit.App", "Resources", "Localization");
    }

    private static IReadOnlySet<string> GetKeys(string resxPath)
    {
        var doc = XDocument.Load(resxPath);
        return doc.Root!
            .Elements("data")
            .Select(e => e.Attribute("name")!.Value)
            .Where(k => !k.StartsWith(">>") && !k.StartsWith("$"))
            .ToHashSet();
    }

    public static TheoryData<string> LanguageFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(LocalizationDir, "AppResources.*.resx").OrderBy(f => f))
            data.Add(Path.GetFileName(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(LanguageFiles))]
    public void AllDefaultKeysArePresentInTranslation(string languageFileName)
    {
        var defaultKeys = GetKeys(Path.Combine(LocalizationDir, "AppResources.resx"));
        var translatedKeys = GetKeys(Path.Combine(LocalizationDir, languageFileName));

        var missing = defaultKeys.Except(translatedKeys).OrderBy(k => k).ToList();

        Assert.True(
            missing.Count == 0,
            $"{languageFileName} is missing {missing.Count} key(s):\n  " + string.Join("\n  ", missing));
    }
}
