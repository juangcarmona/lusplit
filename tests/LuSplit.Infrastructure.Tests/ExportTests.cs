using LuSplit.Application.Export.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Queries;
using LuSplit.Application.Payments.Models;
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

    // ── ResolveSettlementMode parity ──────────────────────────────────────────

    [Fact]
    public void ResolveSettlementMode_NoDepend_ReturnsParticipant()
    {
        // Two independent participants, two separate economic units.
        var overview = MakeOverview(
            participants: [new ParticipantModel("p1", "g", "u1", "Alice", "Full", null),
                           new ParticipantModel("p2", "g", "u2", "Bob",   "Full", null)],
            units:        [new EconomicUnitModel("u1", "g", "p1", null),
                           new EconomicUnitModel("u2", "g", "p2", null)]);

        Assert.Equal(SettlementMode.Participant, overview.ResolveSettlementMode());
    }

    [Fact]
    public void ResolveSettlementMode_WithDependent_ReturnsEconomicUnitOwner()
    {
        // Alice owns unit u1; Charlie is a dependent also in u1. Bob has his own unit.
        var overview = MakeOverview(
            participants: [new ParticipantModel("p-alice",   "g", "u1", "Alice",   "Full", null),
                           new ParticipantModel("p-charlie", "g", "u1", "Charlie", "Half", null),
                           new ParticipantModel("p-bob",     "g", "u2", "Bob",     "Full", null)],
            units:        [new EconomicUnitModel("u1", "g", "p-alice", null),
                           new EconomicUnitModel("u2", "g", "p-bob",   null)]);

        Assert.Equal(SettlementMode.EconomicUnitOwner, overview.ResolveSettlementMode());
    }

    // ── Settlement / balances consistency ────────────────────────────────────

    [Fact]
    public async Task ExportPdf_Settlement_MatchesResolveSettlementPlan_NoDepend()
    {
        var dto = await CreateTestDto(); // Alice paid EUR 10, split Alice+Bob equally → Bob owes Alice EUR 5
        var plan = dto.Overview.ResolveSettlementPlan();

        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);
        var raw = System.Text.Encoding.Latin1.GetString(bytes);

        // Every transfer in the plan must appear in the PDF.
        foreach (var t in plan.Transfers)
        {
            var from = dto.Overview.Participants.First(p => p.Id == t.FromParticipantId).Name;
            var to   = dto.Overview.Participants.First(p => p.Id == t.ToParticipantId).Name;
            Assert.Contains(from, raw);
            Assert.Contains(to,   raw);
        }
    }

    [Fact]
    public async Task ExportCsv_Balances_MatchesResolveBalances_NoDepend()
    {
        var dto = await CreateTestDto();
        var expectedBalances = dto.Overview.ResolveBalances();

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);
        using var zip  = ZipFile.OpenRead(result.FilePath);
        var entry = zip.GetEntry("balances.csv")!;
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();

        // Every entity in the resolved balances must appear in the CSV.
        foreach (var b in expectedBalances)
        {
            var name = dto.Overview.Participants.FirstOrDefault(p => p.Id == b.EntityId)?.Name ?? b.EntityId;
            Assert.Contains(name, content);
        }
    }

    [Fact]
    public async Task ExportPdf_RecordedPayment_ReflectedInSettlement()
    {
        // Alice paid EUR 10 for Bob; then Bob recorded a payment of EUR 5 to Alice.
        // Net: Bob still owes Alice EUR 5.
        var dto = await CreateDtoWithPaymentAsync();
        var plan = dto.Overview.ResolveSettlementPlan();

        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);
        var raw = System.Text.Encoding.Latin1.GetString(bytes);

        // Settlement plan from overview must be shown verbatim.
        if (plan.Transfers.Count == 0)
        {
            Assert.Contains("even", raw, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            foreach (var t in plan.Transfers)
            {
                var from = dto.Overview.Participants.First(p => p.Id == t.FromParticipantId).Name;
                Assert.Contains(from, raw);
            }
        }
    }

    // ── Regression: dependents must be rolled up in export ───────────────────

    [Fact]
    public async Task ExportPdf_WithDependent_SettlementRolledUpToOwner()
    {
        // Bob pays EUR 30 split equally 3 ways (Alice, Charlie, Bob).
        // Charlie is Alice's dependent → App shows "Alice → Bob  EUR 20"
        // (not separate Alice EUR 10 + Charlie EUR 10 lines)
        var dto = await CreateDtoWithDependentAsync();
        var plan = dto.Overview.ResolveSettlementPlan();

        // Verify the overview itself is in EconomicUnitOwner mode.
        Assert.Equal(SettlementMode.EconomicUnitOwner, dto.Overview.ResolveSettlementMode());

        // Verify there is exactly one settlement transfer (Alice → Bob).
        Assert.Single(plan.Transfers);
        var transfer = plan.Transfers[0];
        Assert.Equal("p-alice", transfer.FromParticipantId);
        Assert.Equal("p-bob",   transfer.ToParticipantId);
        Assert.Equal(2000L,      transfer.AmountMinor); // EUR 20.00

        // Verify the PDF reflects this: "Alice" appears as the payer, not "Charlie" separately.
        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);
        var raw = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.Contains("Alice", raw);
        Assert.Contains("Bob",   raw);
        // Charlie should not appear as a settlement payer in the "Remaining Settlement" section.
        // (Charlie still appears in the Participants section, which is correct.)
        // The settlement content is the last distinct section before Expenses.
        // Simplest assertion: check that the amount 20.00 appears in the PDF.
        Assert.Contains("20.00", raw);
    }

    [Fact]
    public async Task ExportCsv_WithDependent_BalancesRolledUpToOwner()
    {
        var dto = await CreateDtoWithDependentAsync();

        var result = await new GroupExporterService().ExportCsvBundleAsync(dto);
        using var zip  = ZipFile.OpenRead(result.FilePath);
        var entry = zip.GetEntry("balances.csv")!;
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();

        // In EconomicUnitOwner mode, Charlie (dependent) must NOT appear as a
        // separate balance row — her debt is aggregated under Alice.
        Assert.DoesNotContain("Charlie", content);
        // Alice (owner) should appear with -20.00 and Bob with +20.00.
        Assert.Contains("Alice", content);
        Assert.Contains("Bob",   content);
        Assert.Contains("-20.00", content);
        Assert.Contains("20.00",  content);
    }

    [Fact]
    public async Task ExportPdf_WithDependent_ParticipantsSectionShowsManagedBy()
    {
        var dto = await CreateDtoWithDependentAsync();

        var bytes = await File.ReadAllBytesAsync(
            (await new GroupExporterService().ExportPdfAsync(dto)).FilePath);
        var raw = System.Text.Encoding.Latin1.GetString(bytes);

        // Charlie is a dependent — the Participants section must say "managed by Alice".
        Assert.Contains("managed by Alice", raw);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<ExportGroupDto> CreateDtoWithPaymentAsync()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(new Group("g-pay", "EUR", false), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", "g-pay", "p1", null), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", "g-pay", "p2", null), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p1", "g-pay", "u1", "Alice", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p2", "g-pay", "u2", "Bob",   ConsumptionCategory.Full), CancellationToken.None);

        // Alice pays EUR 10 shared with Bob → Bob owes Alice EUR 5
        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1", "g-pay", "Dinner", "p1", 1000,
            "2026-01-15T12:00:00.000Z",
            new SplitDefinition([new RemainderSplitComponent(["p1", "p2"], RemainderMode.Equal)]),
            null), CancellationToken.None);

        // Bob pays back EUR 5 — after this the outstanding balance is EUR 0
        await infra.TransferRepository.SaveTransferAsync(
            new LuSplit.Domain.Payments.Transfer(
                "t1", "g-pay", "p2", "p1", 500,
                "2026-01-16T12:00:00.000Z",
                LuSplit.Domain.Payments.TransferType.Manual, null),
            CancellationToken.None);

        var overview = await BuildOverviewAsync(infra, "g-pay");
        var outputDir = NewOutputDir();
        return new ExportGroupDto("g-pay", "Payment Test", "2026-03-14T10:00:00.000Z", overview, outputDir);
    }

    /// <summary>
    /// Group: Alice (owner of u1), Charlie (dependent in u1), Bob (owner of u2).
    /// Bob pays EUR 30 split equally → each owes EUR 10.
    /// In EconomicUnitOwner mode Charlie rolls up to Alice: Alice → Bob EUR 20.
    /// </summary>
    private static async Task<ExportGroupDto> CreateDtoWithDependentAsync()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(new Group("g-dep", "EUR", false), CancellationToken.None);
        // u1: Alice is owner, Charlie is dependent
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", "g-dep", "p-alice", null), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", "g-dep", "p-bob",   null), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p-alice",   "g-dep", "u1", "Alice",   ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p-charlie", "g-dep", "u1", "Charlie", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p-bob",     "g-dep", "u2", "Bob",     ConsumptionCategory.Full), CancellationToken.None);

        // Bob pays EUR 30, split 3 ways equally → Alice: -10, Charlie: -10, Bob: +20
        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1", "g-dep", "Lunch", "p-bob", 3000,
            "2026-01-15T12:00:00.000Z",
            new SplitDefinition([new RemainderSplitComponent(["p-alice", "p-charlie", "p-bob"], RemainderMode.Equal)]),
            null), CancellationToken.None);

        var overview = await BuildOverviewAsync(infra, "g-dep");
        var outputDir = NewOutputDir();
        return new ExportGroupDto("g-dep", "Dependent Test", "2026-03-14T10:00:00.000Z", overview, outputDir);
    }

    private static Task<LuSplit.Application.Groups.Models.GroupOverviewModel> BuildOverviewAsync(
        InfraLocalSqlite infra, string groupId)
        => new GetGroupOverviewUseCase(
            infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository,
            infra.ExpenseRepository, infra.TransferRepository).ExecuteAsync(groupId);

    private static string NewOutputDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lusplit-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static GroupOverviewModel MakeOverview(
        IReadOnlyList<ParticipantModel> participants,
        IReadOnlyList<EconomicUnitModel> units)
        => new GroupOverviewModel(
            new LuSplit.Application.Groups.Models.GroupModel("g", "EUR", false),
            new LuSplit.Application.Groups.Models.GroupSummaryModel("g", participants.Count, units.Count, 0, 0),
            participants,
            units,
            [],
            [],
            [],
            [],
            new SettlementPlanModel(SettlementMode.Participant, []),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));
}
