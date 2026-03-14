using System.Text;
using System.Text.RegularExpressions;

namespace LuSplit.Infrastructure.Export;

internal static class ExportFileNaming
{
    public static string Slug(string tripName, string exportedAt)
    {
        var date = DateTimeOffset.TryParse(exportedAt, null,
            System.Globalization.DateTimeStyles.None, out var dt)
            ? dt.UtcDateTime.ToString("yyyy-MM-dd")
            : "export";

        // Decompose accented chars (é → e + combining mark), strip non-alnum
        var normalized = tripName.Normalize(NormalizationForm.FormD);
        var slug = Regex.Replace(normalized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? $"trip-{date}" : $"{slug}-{date}";
    }
}
