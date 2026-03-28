using System.Text.Json;
using LuSplit.Application.Commands;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Client;
using LuSplit.Infrastructure.Snapshot;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Tests;

public sealed class InfrastructureParityTests
{
    [Fact]
    public async Task SqliteMigrationsAreIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await SqliteMigrations.ApplyAsync(connection);
        await SqliteMigrations.ApplyAsync(connection);

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT COUNT(*) FROM schema_version WHERE version = 1";
        var count = Convert.ToInt32(versionCommand.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SqliteRepositoryContractFlowMatchesExpectedDeterministicResults()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var result = await RunScenarioAsync(infra);

        Assert.Equal(
            new[]
            {
                new BalanceModel("id-4", 450),
                new BalanceModel("id-5", 50),
                new BalanceModel("id-6", -500)
            },
            result.BalancesByParticipant);

        Assert.Equal(
            new[]
            {
                new BalanceModel("id-4", 450),
                new BalanceModel("id-5", -450)
            },
            result.BalancesByOwner);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel("id-6", "id-4", 450),
                new SettlementTransferModel("id-6", "id-5", 50)
            },
            result.SettlementByParticipant.Transfers);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel("id-5", "id-4", 450)
            },
            result.SettlementByOwner.Transfers);

        Assert.Equal(1, result.Overview.Summary.TransferCount);
    }

    [Fact]
    public async Task SqlitePreservesDeterministicResultsAfterReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lusplit-m3-{Guid.NewGuid():N}.sqlite");

        using (var first = await InfraLocalSqlite.CreateAsync(path))
        {
            await RunScenarioAsync(first);
        }

        GroupOverviewModel overview;
        using (var second = await InfraLocalSqlite.CreateAsync(path))
        {
            overview = await new GetGroupOverviewUseCase(
                second.GroupRepository,
                second.ParticipantRepository,
                second.EconomicUnitRepository,
                second.ExpenseRepository,
                second.TransferRepository).ExecuteAsync("id-1");
        }

        Assert.Equal(
            new[]
            {
                new BalanceModel("id-4", 450),
                new BalanceModel("id-5", 50),
                new BalanceModel("id-6", -500)
            },
            overview.BalancesByParticipant);

        File.Delete(path);
    }

    [Fact]
    public async Task SqliteDeterminismIsIndependentFromInsertionOrder()
    {
        var first = await WriteScenarioInOrderAsync('A');
        var second = await WriteScenarioInOrderAsync('B');

        Assert.Equal(first.Balances, second.Balances);
        Assert.Equal(first.Settlement.Mode, second.Settlement.Mode);
        Assert.Equal(first.Settlement.Transfers, second.Settlement.Transfers);
    }

    [Fact]
    public async Task SqliteExportImportRoundGroupPreservesBalancesAndSettlement()
    {
        using var source = await InfraLocalSqlite.CreateAsync();
        var sourceResult = await RunScenarioAsync(source);

        var snapshot = await source.ExportGroupSnapshotAsync(sourceResult.GroupId);

        using var target = await InfraLocalSqlite.CreateAsync();
        await target.ImportGroupSnapshotAsync(snapshot);

        var imported = await new GetGroupOverviewUseCase(
            target.GroupRepository,
            target.ParticipantRepository,
            target.EconomicUnitRepository,
            target.ExpenseRepository,
            target.TransferRepository).ExecuteAsync(sourceResult.GroupId);

        Assert.Equal(sourceResult.Overview.BalancesByParticipant, imported.BalancesByParticipant);
        Assert.Equal(sourceResult.Overview.BalancesByEconomicUnitOwner, imported.BalancesByEconomicUnitOwner);
        Assert.Equal(sourceResult.Overview.SettlementByParticipant.Mode, imported.SettlementByParticipant.Mode);
        Assert.Equal(sourceResult.Overview.SettlementByParticipant.Transfers, imported.SettlementByParticipant.Transfers);
        Assert.Equal(sourceResult.Overview.SettlementByEconomicUnitOwner.Mode, imported.SettlementByEconomicUnitOwner.Mode);
        Assert.Equal(sourceResult.Overview.SettlementByEconomicUnitOwner.Transfers, imported.SettlementByEconomicUnitOwner.Transfers);

        var reExport = await target.ExportGroupSnapshotAsync(sourceResult.GroupId);
        Assert.Equal(ToCanonicalJson(snapshot), ToCanonicalJson(reExport));
    }

    [Fact]
    public async Task SqliteImportRejectsMalformedSnapshotReferences()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        var malformed = new
        {
            version = 1,
            group = new { id = "g1", currency = "USD", closed = false },
            participants = new[]
            {
                new
                {
                    id = "p1",
                    groupId = "other-group",
                    economicUnitId = "u1",
                    name = "Alice",
                    consumptionCategory = "FULL"
                }
            },
            economicUnits = new[]
            {
                new { id = "u1", groupId = "g1", ownerParticipantId = "p1" }
            },
            expenses = Array.Empty<object>(),
            transfers = Array.Empty<object>()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => infra.ImportGroupSnapshotAsync(malformed));
    }

    [Fact]
    public async Task SqliteRepositoriesEnforceGloballyUniqueIdsAcrossGroups()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(new Group("g1", "USD", false), CancellationToken.None);
        await infra.GroupRepository.SaveGroupAsync(new Group("g2", "USD", false), CancellationToken.None);

        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", "g1", "p1", "Unit 1"), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", "g2", "p2", "Unit 2"), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", "g2", "p2", "Unit 1 clone"), CancellationToken.None));

        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p2", "g2", "u2", "Bob", ConsumptionCategory.Full), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            infra.ParticipantRepository.SaveParticipantAsync(new Participant("p1", "g2", "u2", "Alice Clone", ConsumptionCategory.Full), CancellationToken.None));

        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1",
            "g1",
            "Expense 1",
            "p1",
            100,
            "2026-01-01T00:00:00.000Z",
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1" }, RemainderMode.Equal)
            })), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            infra.ExpenseRepository.SaveAsync(new Expense(
                "e1",
                "g2",
                "Expense 1 clone",
                "p2",
                100,
                "2026-01-01T00:00:00.000Z",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "p2" }, RemainderMode.Equal)
                })), CancellationToken.None));

        await infra.TransferRepository.SaveTransferAsync(new Transfer("t1", "g1", "p1", "p1", 50, "2026-01-01T00:00:00.000Z", TransferType.Manual), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            infra.TransferRepository.SaveTransferAsync(new Transfer("t1", "g2", "p2", "p2", 50, "2026-01-01T00:00:00.000Z", TransferType.Manual), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteImportRejectsUnsupportedVersionAndMissingFields()
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => infra.ImportGroupSnapshotAsync(new { version = 2 }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => infra.ImportGroupSnapshotAsync(new { version = 1 }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => infra.ImportGroupSnapshotAsync(new { version = 1, group = new { id = "g1", currency = "USD", closed = false } }));
    }

    private static async Task<(
        string GroupId,
        IReadOnlyList<BalanceModel> BalancesByParticipant,
        IReadOnlyList<BalanceModel> BalancesByOwner,
        SettlementPlanModel SettlementByParticipant,
        SettlementPlanModel SettlementByOwner,
        GroupOverviewModel Overview)> RunScenarioAsync(InfraLocalSqlite infra)
    {
        var idGenerator = new SequentialIdGenerator();
        var clock = new FixedClock("2026-01-01T12:00:00.000Z");

        var createGroup = new CreateGroupUseCase(infra.GroupRepository, idGenerator);
        var createEconomicUnit = new CreateEconomicUnitUseCase(infra.GroupRepository, infra.EconomicUnitRepository, idGenerator);
        var createParticipant = new CreateParticipantUseCase(infra.GroupRepository, infra.EconomicUnitRepository, infra.ParticipantRepository, idGenerator);
        var addExpense = new AddExpenseUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.ExpenseRepository, idGenerator, clock);
        var addManualTransfer = new AddManualTransferUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.TransferRepository, idGenerator, clock);

        var group = await createGroup.ExecuteAsync(new CreateGroupInput("USD"));
        var unit1 = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-4", "Unit 1"));
        var unit2 = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-5", "Unit 2"));

        var p1 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit1.Id, "Alice", ConsumptionCategory.Full));
        var p2 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit2.Id, "Bob", ConsumptionCategory.Full));
        var p3 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit2.Id, "Carol", ConsumptionCategory.Half));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            group.Id,
            "Dinner",
            p1.Id,
            900,
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { p1.Id, p2.Id, p3.Id }, RemainderMode.Equal)
            })));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            group.Id,
            "Groceries",
            p2.Id,
            600,
            new SplitDefinition(new SplitComponent[]
            {
                new FixedSplitComponent(new Dictionary<string, long>
                {
                    [p1.Id] = 100
                }),
                new RemainderSplitComponent(new[] { p2.Id, p3.Id }, RemainderMode.Equal)
            })));

        await addManualTransfer.ExecuteAsync(new AddManualTransferInput(group.Id, p3.Id, p1.Id, 50));

        var balancesByParticipant = await new GetBalancesByParticipantUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(group.Id);

        var balancesByOwner = await new GetBalancesByEconomicUnitOwnerUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(group.Id);

        var settlementByParticipant = await new GetSettlementPlanUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(group.Id, SettlementMode.Participant);

        var settlementByOwner = await new GetSettlementPlanUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(group.Id, SettlementMode.EconomicUnitOwner);

        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(group.Id);

        return (group.Id, balancesByParticipant, balancesByOwner, settlementByParticipant, settlementByOwner, overview);
    }

    private static async Task<(IReadOnlyList<BalanceModel> Balances, SettlementPlanModel Settlement)> WriteScenarioInOrderAsync(char order)
    {
        using var infra = await InfraLocalSqlite.CreateAsync();

        await infra.GroupRepository.SaveGroupAsync(new Group("g1", "USD", false), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", "g1", "p1", "Unit 1"), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", "g1", "p2", "Unit 2"), CancellationToken.None);

        var participants = new[]
        {
            new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full),
            new Participant("p2", "g1", "u2", "Bob", ConsumptionCategory.Full),
            new Participant("p3", "g1", "u2", "Carol", ConsumptionCategory.Half)
        };

        var expenses = new[]
        {
            new Expense(
                "e1",
                "g1",
                "Dinner",
                "p1",
                900,
                "2026-01-01T00:00:00.000Z",
                new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(new[] { "p1", "p2", "p3" }, RemainderMode.Equal)
                })),
            new Expense(
                "e2",
                "g1",
                "Groceries",
                "p2",
                600,
                "2026-01-02T00:00:00.000Z",
                new SplitDefinition(new SplitComponent[]
                {
                    new FixedSplitComponent(new Dictionary<string, long>
                    {
                        ["p1"] = 100
                    }),
                    new RemainderSplitComponent(new[] { "p2", "p3" }, RemainderMode.Equal)
                }))
        };

        if (order == 'A')
        {
            await infra.ParticipantRepository.SaveParticipantAsync(participants[0], CancellationToken.None);
            await infra.ParticipantRepository.SaveParticipantAsync(participants[1], CancellationToken.None);
            await infra.ParticipantRepository.SaveParticipantAsync(participants[2], CancellationToken.None);
            await infra.ExpenseRepository.SaveAsync(expenses[0], CancellationToken.None);
            await infra.ExpenseRepository.SaveAsync(expenses[1], CancellationToken.None);
        }
        else
        {
            await infra.ParticipantRepository.SaveParticipantAsync(participants[2], CancellationToken.None);
            await infra.ParticipantRepository.SaveParticipantAsync(participants[0], CancellationToken.None);
            await infra.ParticipantRepository.SaveParticipantAsync(participants[1], CancellationToken.None);
            await infra.ExpenseRepository.SaveAsync(expenses[1], CancellationToken.None);
            await infra.ExpenseRepository.SaveAsync(expenses[0], CancellationToken.None);
        }

        var balances = await new GetBalancesByParticipantUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.ExpenseRepository, infra.TransferRepository)
            .ExecuteAsync("g1");

        var settlement = await new GetSettlementPlanUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository, infra.ExpenseRepository, infra.TransferRepository)
            .ExecuteAsync("g1", SettlementMode.Participant);

        return (balances, settlement);
    }

    private static string ToCanonicalJson(object value)
        => JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

    private sealed class SequentialIdGenerator : LuSplit.Application.Ports.IIdGenerator
    {
        private int _current;

        public string NextId()
        {
            _current += 1;
            return $"id-{_current}";
        }
    }

    private sealed class FixedClock : LuSplit.Application.Ports.IClock
    {
        private readonly string _nowIso;

        public FixedClock(string nowIso)
        {
            _nowIso = nowIso;
        }

        public string NowIso() => _nowIso;
    }
}
