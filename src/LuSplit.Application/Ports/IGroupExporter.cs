using LuSplit.Application.Models;

namespace LuSplit.Application.Ports;

public interface IGroupExporter
{
    Task<ExportFileResult> ExportJsonAsync(ExportGroupDto dto, CancellationToken ct = default);
    Task<ExportFileResult> ExportCsvBundleAsync(ExportGroupDto dto, CancellationToken ct = default);
    Task<ExportFileResult> ExportPdfAsync(ExportGroupDto dto, CancellationToken ct = default);
}
