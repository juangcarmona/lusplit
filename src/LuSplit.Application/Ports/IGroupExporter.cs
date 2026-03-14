using LuSplit.Application.Models;

namespace LuSplit.Application.Ports;

public interface IGroupExporter
{
    Task<ExportFileResult> ExportJsonAsync(ExportTripDto dto, CancellationToken ct = default);
    Task<ExportFileResult> ExportCsvBundleAsync(ExportTripDto dto, CancellationToken ct = default);
    Task<ExportFileResult> ExportPdfAsync(ExportTripDto dto, CancellationToken ct = default);
}
