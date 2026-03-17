namespace LuSplit.Application.Models;

public enum ExportFormat { Json, Csv, Pdf }

public sealed record ExportFileResult(string FilePath, string FileName, string MimeType);

public sealed record ExportGroupDto(
    string GroupId,
    string GroupName,
    string ExportedAt,
    GroupOverviewModel Overview,
    string OutputDirectory);
