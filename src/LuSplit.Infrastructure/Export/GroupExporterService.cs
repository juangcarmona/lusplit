using LuSplit.Application.Export.Models;
using LuSplit.Application.Export.Ports;

namespace LuSplit.Infrastructure.Export;

public sealed class GroupExporterService : IGroupExporter
{
    private readonly JsonGroupExporter _json = new();
    private readonly CsvGroupExporter _csv = new();
    private readonly PdfGroupExporter _pdf = new();

    public Task<ExportFileResult> ExportJsonAsync(ExportGroupDto dto, CancellationToken ct = default)
        => _json.ExportAsync(dto, ct);

    public Task<ExportFileResult> ExportCsvBundleAsync(ExportGroupDto dto, CancellationToken ct = default)
        => _csv.ExportAsync(dto, ct);

    public Task<ExportFileResult> ExportPdfAsync(ExportGroupDto dto, CancellationToken ct = default)
        => _pdf.ExportAsync(dto, ct);
}
