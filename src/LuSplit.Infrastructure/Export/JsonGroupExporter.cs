using System.Text;
using System.Text.Json;
using LuSplit.Application.Export.Models;
using LuSplit.Infrastructure.Snapshot;
using LuSplit.Infrastructure.Sqlite;

namespace LuSplit.Infrastructure.Export;

internal sealed class JsonGroupExporter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public async Task<ExportFileResult> ExportAsync(ExportGroupDto dto, CancellationToken ct)
    {
        var root = BuildRoot(dto);
        var json = JsonSerializer.Serialize(root, WriteOptions);

        var slug = ExportFileNaming.Slug(dto.GroupName, dto.ExportedAt);
        var fileName = $"{slug}.snapshot.json";
        var filePath = Path.Combine(dto.OutputDirectory, fileName);

        await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

        return new ExportFileResult(filePath, fileName, "application/json");
    }

    private static object BuildRoot(ExportGroupDto dto)
    {
        var o = dto.Overview;
        return new
        {
            schemaVersion = SnapshotContract.Version,
            groupName = dto.GroupName,
            exportedAt = dto.ExportedAt,
            group = new
            {
                id = o.Group.Id,
                currency = o.Group.Currency,
                closed = o.Group.Closed
            },
            participants = o.Participants.Select(p => new
            {
                id = p.Id,
                groupId = p.GroupId,
                economicUnitId = p.EconomicUnitId,
                name = p.Name,
                consumptionCategory = p.ConsumptionCategory,
                customConsumptionWeight = p.CustomConsumptionWeight
            }).ToArray(),
            economicUnits = o.EconomicUnits.Select(u => new
            {
                id = u.Id,
                groupId = u.GroupId,
                ownerParticipantId = u.OwnerParticipantId,
                name = u.Name
            }).ToArray(),
            expenses = o.Expenses.Select(e => new
            {
                id = e.Id,
                groupId = e.GroupId,
                title = e.Title,
                paidByParticipantId = e.PaidByParticipantId,
                amountMinor = e.AmountMinor,
                date = e.Date,
                splitDefinition = SplitJson.SerializeDefinitionToElement(e.SplitDefinition),
                notes = e.Notes
            }).ToArray(),
            transfers = o.Transfers.Select(t => new
            {
                id = t.Id,
                groupId = t.GroupId,
                fromParticipantId = t.FromParticipantId,
                toParticipantId = t.ToParticipantId,
                amountMinor = t.AmountMinor,
                date = t.Date,
                type = t.Type,
                note = t.Note
            }).ToArray()
        };
    }
}
