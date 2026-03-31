using LuSplit.Application.Export.Models;
using LuSplit.Application.Groups.Queries;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Infrastructure.Export;
using LuSplit.Infrastructure.Sqlite;
using System.IO.Compression;
using System.Text.Json;

namespace LuSplit.Infrastructure.Tests;

public sealed class ExportTests
{
    [Fact]
    public async Task ExportJson_ContainsAllExpectedTopLevelFields()
    {
        var dto = await CreateTestDto();
        var exporter = new GroupExporterService();

        var result = await exporter.ExportJsonAsync(dto);

        Assert.True(File.Exists(result.FilePath));
        Assert.Equal("application/json", result.MimeType);
        Assert.EndsWith(".snapshot.json", result.FileName);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(result.FilePath));
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Test Group", root.GetProperty("groupName").GetString());
        Assert.Equal(2, root.GetProperty("participants").GetArrayLength());
        Assert.Equal(1, root.GetProperty("expenses").GetArrayLength());
        Assert.Equal(0, root.GetProperty("transfers").GetArrayLength());
    }

    [Fact]
    public async Task ExportJson_PreservesAmountsInMinorUnits()
    {
        var dto = await CreateTestDto(); // expense amountMinor = 1000

        var result = await new GroupExporterService().ExportJsonAsync(dto);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(result.FilePath));
        var expense = doc.RootElement.GetProperty("expenses")[0];
        Assert.Equal(1000, expense.GetProperty("amountMinor").GetInt64());
    }

    [Fact]
    public async Task ExportJson_FileNameIsSlugifiedGroupName()
    {
        var dto = await CreateTestDto("Weekend in Paris!");

        var result = await new GroupExporterService().ExportJsonAsync(dto);

        Assert.Contains("weekend-in-paris", result.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".snapshot.json", result.FileName);
    }

    [Fact]
    public async Task ExportCsv_ZipContainsAllFourCsvFiles()
    {
        var dto = await CreateTestDto();

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);

        Assert.True(File.Exists(result.FilePath));
        Assert.Equal("application/zip", result.MimeType);
        Assert.EndsWith("-export.zip", result.FileName);

        using var zip = ZipFile.OpenRead(result.FilePath);
        var entries = zip.Entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("expenses.csv", entries);
        Assert.Contains("members.csv", entries);
        Assert.Contains("transfers.csv", entries);
        Assert.Contains("balances.csv", entries);
    }

    [Fact]
    public async Task ExportCsv_ExpensesCsvHasHeaderPlusOneRow()
    {
        var dto = await CreateTestDto();

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);

        using var zip = ZipFile.OpenRead(result.FilePath);
        var entry = zip.GetEntry("expenses.csv")!;
        using var reader = new StreamReader(entry.Open());
        var lines = (await reader.ReadToEndAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length); // header row + 1 data row
        Assert.Contains("title", lines[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportCsv_MoneyIsFormattedAsDecimalNotMinorUnits()
    {
        var dto = await CreateTestDto(); // expense amountMinor = 1000 → "10.00"

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);

        using var zip = ZipFile.OpenRead(result.FilePath);
        var entry = zip.GetEntry("expenses.csv")!;
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();

        Assert.Contains("10.00", content);
        Assert.DoesNotContain(",1000,", content); // minor units must not appear as a standalone CSV field
    }

    [Fact]
    public async Task ExportCsv_MembersCsvContainsParticipantNames()
    {
        var dto = await CreateTestDto();

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);

        using var zip = ZipFile.OpenRead(result.FilePath);
        var entry = zip.GetEntry("members.csv")!;
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();

        Assert.Contains("Alice", content);
        Assert.Contains("Bob", content);
    }

    [Fact]
    public async Task ExportPdf_ProducesValidPdfFile()
    {
        var dto = await CreateTestDto();

        var result = await new GroupExporterService().ExportPdfAsync(dto);

        Assert.True(File.Exists(result.FilePath));
        Assert.Equal("application/pdf", result.MimeType);
        Assert.EndsWith("-summary.pdf", result.FileName);

        var bytes = await File.ReadAllBytesAsync(result.FilePath);
        Assert.True(bytes.Length > 200, "PDF should not be trivially empty");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task ExportPdf_ContainsGroupNameInContent()
    {
        var dto = await CreateTestDto("My Mediterranean Group");

        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);

        // Group name must appear in the PDF content stream (Latin-1 encoded)
        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("My Mediterranean Group", raw);
    }

    [Fact]
    public async Task ExportPdf_EmptyGroupShowsAllSettledMessage()
    {
        var dto = await CreateEmptyGroupDto();
        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);

        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("even", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<ExportGroupDto> CreateTestDto(string groupName = "Test Group")
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(
            new Group("g-exp", "EUR", false), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(
            new EconomicUnit("u1", "g-exp", "p1", null), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(
            new EconomicUnit("u2", "g-exp", "p2", null), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant("p1", "g-exp", "u1", "Alice", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant("p2", "g-exp", "u2", "Bob", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1", "g-exp", "Dinner", "p1", 1000,
            "2026-01-15T12:00:00.000Z",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2" }, RemainderMode.Equal)
            }),
            null), CancellationToken.None);

        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository,
            infra.ExpenseRepository, infra.TransferRepository).ExecuteAsync("g-exp");

        var outputDir = Path.Combine(Path.GetTempPath(), $"lusplit-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        return new ExportGroupDto("g-exp", groupName, "2026-03-14T10:00:00.000Z", overview, outputDir);
    }

    private static async Task<ExportGroupDto> CreateEmptyGroupDto()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(
            new Group("g-empty", "USD", false), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(
            new EconomicUnit("u1", "g-empty", "p1", null), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(
            new Participant("p1", "g-empty", "u1", "Alex", ConsumptionCategory.Full), CancellationToken.None);

        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository,
            infra.ExpenseRepository, infra.TransferRepository).ExecuteAsync("g-empty");

        var outputDir = Path.Combine(Path.GetTempPath(), $"lusplit-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        return new ExportGroupDto("g-empty", "Empty Group", "2026-03-14T10:00:00.000Z", overview, outputDir);
    }
}
