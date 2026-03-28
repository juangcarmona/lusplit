using System.Globalization;
using System.IO.Compression;
using System.Text;
using LuSplit.Application.Models;

namespace LuSplit.Infrastructure.Export;

internal sealed class CsvGroupExporter
{
    public async Task<ExportFileResult> ExportAsync(ExportGroupDto dto, CancellationToken ct)
    {
        var slug = ExportFileNaming.Slug(dto.GroupName, dto.ExportedAt);
        var fileName = $"{slug}-export.zip";
        var filePath = Path.Combine(dto.OutputDirectory, fileName);
        var tempPath = filePath + ".tmp";

        try
        {
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                AddEntry(archive, "members.csv", BuildMembers(dto.Overview));
                AddEntry(archive, "expenses.csv", BuildExpenses(dto.Overview));
                AddEntry(archive, "transfers.csv", BuildTransfers(dto.Overview));
                AddEntry(archive, "balances.csv", BuildBalances(dto.Overview));
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        await Task.CompletedTask;
        return new ExportFileResult(filePath, fileName, "application/zip");
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        // UTF-8 with BOM so Excel / Numbers open correctly
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.Write(content);
    }

    private static string BuildMembers(GroupOverviewModel o)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("id", "name", "household", "consumption_category", "custom_weight"));
        foreach (var p in o.Participants.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var household = o.EconomicUnits
                .FirstOrDefault(u => string.Equals(u.Id, p.EconomicUnitId, StringComparison.Ordinal))
                ?.Name ?? string.Empty;
            sb.AppendLine(Row(p.Id, p.Name, household, p.ConsumptionCategory, p.CustomConsumptionWeight ?? string.Empty));
        }
        return sb.ToString();
    }

    private static string BuildExpenses(GroupOverviewModel o)
    {
        var byId = o.Participants.ToDictionary(p => p.Id, p => p.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.AppendLine(Row("id", "title", "amount", "currency", "paid_by", "date", "notes"));
        foreach (var e in o.Expenses.OrderBy(e => e.Date, StringComparer.Ordinal))
        {
            sb.AppendLine(Row(
                e.Id,
                e.Title,
                Money(e.AmountMinor),
                o.Group.Currency,
                byId.GetValueOrDefault(e.PaidByParticipantId, e.PaidByParticipantId),
                e.Date,
                e.Notes ?? string.Empty));
        }
        return sb.ToString();
    }

    private static string BuildTransfers(GroupOverviewModel o)
    {
        var byId = o.Participants.ToDictionary(p => p.Id, p => p.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.AppendLine(Row("id", "from", "to", "amount", "currency", "date", "type", "note"));
        foreach (var t in o.Transfers.OrderBy(t => t.Date, StringComparer.Ordinal))
        {
            sb.AppendLine(Row(
                t.Id,
                byId.GetValueOrDefault(t.FromParticipantId, t.FromParticipantId),
                byId.GetValueOrDefault(t.ToParticipantId, t.ToParticipantId),
                Money(t.AmountMinor),
                o.Group.Currency,
                t.Date,
                t.Type,
                t.Note ?? string.Empty));
        }
        return sb.ToString();
    }

    private static string BuildBalances(GroupOverviewModel o)
    {
        var byId = o.Participants.ToDictionary(p => p.Id, p => p.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.AppendLine(Row("person", "balance", "currency", "direction"));
        foreach (var b in o.BalancesByParticipant.OrderBy(b => b.EntityId, StringComparer.Ordinal))
        {
            var name = byId.GetValueOrDefault(b.EntityId, b.EntityId);
            var direction = b.AmountMinor > 0 ? "is owed" : b.AmountMinor < 0 ? "owes" : "even";
            sb.AppendLine(Row(name, Money(b.AmountMinor), o.Group.Currency, direction));
        }
        return sb.ToString();
    }

    private static string Money(long minor)
        => (minor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Row(params string[] fields)
        => string.Join(",", fields.Select(Escape));

    private static string Escape(string s)
    {
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
