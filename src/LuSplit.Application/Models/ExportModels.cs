namespace LuSplit.Application.Models;

public enum ExportFormat { Json, Csv, Pdf }

public sealed record ExportFileResult(string FilePath, string FileName, string MimeType);

public sealed record ExportTripDto(
    string GroupId,
    string TripName,
    string ExportedAt,
    GroupOverviewModel Overview,
    string OutputDirectory);
