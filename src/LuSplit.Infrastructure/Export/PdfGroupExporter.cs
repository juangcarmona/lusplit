using System.Globalization;
using LuSplit.Application.Export.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Payments.Models;

namespace LuSplit.Infrastructure.Export;

internal sealed class PdfGroupExporter
{
    public async Task<ExportFileResult> ExportAsync(ExportGroupDto dto, CancellationToken ct)
    {
        var lines = BuildLines(dto);
        var pdfBytes = SimplePdfBuilder.Build(lines);

        var slug = ExportFileNaming.Slug(dto.GroupName, dto.ExportedAt);
        var fileName = $"{slug}-summary.pdf";
        var filePath = Path.Combine(dto.OutputDirectory, fileName);

        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);
        return new ExportFileResult(filePath, fileName, "application/pdf");
    }

    private static IReadOnlyList<PdfLine> BuildLines(ExportGroupDto dto)
    {
        var o = dto.Overview;
        var byId = o.Participants.ToDictionary(p => p.Id, p => p.Name, StringComparer.Ordinal);
        var currency = o.Group.Currency;
        // Use the same mode-resolution logic that the app screens use so the PDF
        // reflects exactly the same final state the user sees.
        var mode = o.ResolveSettlementMode();
        var lines = new List<PdfLine>();

        // ── 1. Header ────────────────────────────────────────────
        lines.Add(new PdfLine("LuSplit", PdfLineStyle.Title));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(dto.GroupName, PdfLineStyle.Heading));
        lines.Add(new PdfLine($"Exported on {FormatLongDate(dto.ExportedAt)}  \u00b7  Currency: {currency}", PdfLineStyle.Muted));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 2. Summary ───────────────────────────────────────────
        lines.Add(new PdfLine("Summary", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        var totalMinor = o.Expenses.Sum(e => e.AmountMinor);
        lines.Add(new PdfLine($"  People:    {o.Summary.ParticipantCount}", PdfLineStyle.Normal));
        lines.Add(new PdfLine($"  Expenses:  {o.Summary.ExpenseCount}", PdfLineStyle.Normal));
        lines.Add(new PdfLine($"  Payments:  {o.Summary.TransferCount}", PdfLineStyle.Normal));
        lines.Add(new PdfLine($"  Total:     {FormatMoney(totalMinor, currency)}", PdfLineStyle.Normal));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 3. Participants ──────────────────────────────────────
        lines.Add(new PdfLine("Participants", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        foreach (var p in o.Participants.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var unit = o.EconomicUnits.FirstOrDefault(u =>
                string.Equals(u.Id, p.EconomicUnitId, StringComparison.Ordinal));
            var isDependent = unit is not null &&
                !string.Equals(unit.OwnerParticipantId, p.Id, StringComparison.Ordinal);
            if (isDependent && byId.TryGetValue(unit!.OwnerParticipantId, out var ownerName))
                lines.Add(new PdfLine($"  {p.Name}  (managed by {ownerName})", PdfLineStyle.Normal));
            else
                lines.Add(new PdfLine($"  {p.Name}", PdfLineStyle.Normal));
        }
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 4. Final Balances ────────────────────────────────────
        lines.Add(new PdfLine("Final Balances", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        var balances = o.ResolveBalances();
        var nonZero = balances
            .Where(b => b.AmountMinor != 0)
            .OrderByDescending(b => b.AmountMinor)
            .ToArray();
        if (nonZero.Length == 0)
        {
            lines.Add(new PdfLine("  Everyone is even.", PdfLineStyle.Normal));
        }
        else
        {
            foreach (var b in nonZero)
            {
                var name = ResolveEntityName(b.EntityId, o, mode, byId);
                var sign = b.AmountMinor > 0 ? "+" : string.Empty;
                lines.Add(new PdfLine($"  {name}   {sign}{FormatMoney(b.AmountMinor, currency)}", PdfLineStyle.Normal));
            }
        }
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 5. Remaining Settlement ──────────────────────────────
        lines.Add(new PdfLine("Remaining Settlement", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        var plan = o.ResolveSettlementPlan();
        if (plan.Transfers.Count == 0)
        {
            lines.Add(new PdfLine("  Nothing left to settle. All even.", PdfLineStyle.Normal));
        }
        else
        {
            foreach (var t in plan.Transfers)
            {
                var from = ResolveEntityName(t.FromParticipantId, o, mode, byId);
                var to = ResolveEntityName(t.ToParticipantId, o, mode, byId);
                lines.Add(new PdfLine($"  {from}  \u2192  {to}   {FormatMoney(t.AmountMinor, currency)}", PdfLineStyle.Normal));
            }
        }
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 6. Expenses ──────────────────────────────────────────
        lines.Add(new PdfLine("Expenses", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        if (o.Expenses.Count == 0)
        {
            lines.Add(new PdfLine("  No expenses recorded.", PdfLineStyle.Normal));
        }
        else
        {
            foreach (var e in o.Expenses.OrderBy(e => e.Date, StringComparer.Ordinal))
            {
                var paidBy = byId.GetValueOrDefault(e.PaidByParticipantId, e.PaidByParticipantId);
                lines.Add(new PdfLine($"  {e.Title}", PdfLineStyle.Normal));
                lines.Add(new PdfLine(
                    $"  {paidBy} paid {FormatMoney(e.AmountMinor, currency)}  \u00b7  {FormatShortDate(e.Date)}",
                    PdfLineStyle.Muted));
                if (!string.IsNullOrWhiteSpace(e.Notes))
                    lines.Add(new PdfLine($"  {e.Notes}", PdfLineStyle.Muted));
                lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
            }
        }
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 7. Payments ──────────────────────────────────────────
        lines.Add(new PdfLine("Payments", PdfLineStyle.Heading));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        if (o.Transfers.Count == 0)
        {
            lines.Add(new PdfLine("  No payments recorded.", PdfLineStyle.Normal));
        }
        else
        {
            foreach (var t in o.Transfers.OrderBy(t => t.Date, StringComparer.Ordinal))
            {
                var from = byId.GetValueOrDefault(t.FromParticipantId, t.FromParticipantId);
                var to = byId.GetValueOrDefault(t.ToParticipantId, t.ToParticipantId);
                lines.Add(new PdfLine(
                    $"  {from}  \u2192  {to}   {FormatMoney(t.AmountMinor, currency)}  \u00b7  {FormatShortDate(t.Date)}",
                    PdfLineStyle.Normal));
                if (!string.IsNullOrWhiteSpace(t.Note))
                    lines.Add(new PdfLine($"  {t.Note}", PdfLineStyle.Muted));
            }
        }
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));

        // ── 8. Footer ────────────────────────────────────────────
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Separator));
        lines.Add(new PdfLine(string.Empty, PdfLineStyle.Empty));
        lines.Add(new PdfLine("Generated by LuSplit", PdfLineStyle.Muted));

        return lines;
    }

    /// <summary>
    /// Resolves a display name for a balance or settlement entity ID.
    /// In Participant mode the ID is always a participant ID.
    /// In EconomicUnitOwner mode the ID is an owner participant ID; if the unit
    /// has a custom name it is used, otherwise the owner's participant name is used.
    /// </summary>
    private static string ResolveEntityName(
        string entityId,
        GroupOverviewModel o,
        SettlementMode mode,
        IReadOnlyDictionary<string, string> byId)
    {
        if (mode == SettlementMode.Participant)
            return byId.GetValueOrDefault(entityId, entityId);

        var unit = o.EconomicUnits.FirstOrDefault(u =>
            string.Equals(u.OwnerParticipantId, entityId, StringComparison.Ordinal));
        if (unit is not null && !string.IsNullOrWhiteSpace(unit.Name))
            return unit.Name!;
        return byId.GetValueOrDefault(entityId, entityId);
    }

    private static string FormatMoney(long minor, string currency)
        => $"{currency} {(minor / 100m).ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string FormatLongDate(string isoDate)
    {
        if (DateTimeOffset.TryParse(isoDate, null, DateTimeStyles.None, out var dt))
            return dt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        return isoDate;
    }

    private static string FormatShortDate(string isoDate)
    {
        if (DateTimeOffset.TryParse(isoDate, null, DateTimeStyles.None, out var dt))
            return dt.ToString("MMM d", CultureInfo.InvariantCulture);
        return isoDate;
    }
}
