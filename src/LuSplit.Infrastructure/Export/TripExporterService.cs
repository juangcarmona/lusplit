using LuSplit.Application.Models;
using LuSplit.Application.Ports;

namespace LuSplit.Infrastructure.Export;

public sealed class TripExporterService : IGroupExporter
{
    private readonly JsonTripExporter _json = new();
    private readonly CsvTripExporter _csv = new();
    private readonly PdfTripExporter _pdf = new();

    public Task<ExportFileResult> ExportJsonAsync(ExportTripDto dto, CancellationToken ct = default)
        => _json.ExportAsync(dto, ct);

    public Task<ExportFileResult> ExportCsvBundleAsync(ExportTripDto dto, CancellationToken ct = default)
        => _csv.ExportAsync(dto, ct);

    public Task<ExportFileResult> ExportPdfAsync(ExportTripDto dto, CancellationToken ct = default)
        => _pdf.ExportAsync(dto, ct);
}
