using LuSplit.Application.Commands;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Client;

namespace LuSplit.App.Services;

public sealed class AppDataService : IAsyncDisposable
{
    private const string DefaultGroupId = "g1";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private InfraLocalSqlite? _infra;
    private string? _lastPaidByParticipantId;
    private IReadOnlyList<string> _lastParticipantIds = Array.Empty<string>();

    public event EventHandler? DataChanged;

    public async Task InitializeAsync()
    {
        if (_infra is not null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_infra is not null)
            {
                return;
            }

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lusplit.sqlite");
            _infra = await InfraLocalSqlite.CreateAsync(dbPath);
            await EnsureSeedDataAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GroupOverviewModel> GetOverviewAsync()
    {
        var infra = await GetInfraAsync();
        return await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(DefaultGroupId);
    }

    public async Task<IReadOnlyList<ParticipantModel>> GetParticipantsAsync()
    {
        var overview = await GetOverviewAsync();
        return overview.Participants;
    }

    public EventDraftDefaults GetEventDraftDefaults()
        => new(_lastPaidByParticipantId, _lastParticipantIds);

    public async Task AddExpenseAsync(string title, long amountMinor, string paidByParticipantId, DateTime date, IReadOnlyList<string> participantIds)
    {
        var infra = await GetInfraAsync();

        var split = new SplitDefinition(new SplitComponent[]
        {
            new RemainderSplitComponent(participantIds, RemainderMode.Equal)
        });

        await new AddExpenseUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.ExpenseRepository,
            new GuidIdGenerator(),
            new UtcClock()).ExecuteAsync(new AddExpenseInput(
                GroupId: DefaultGroupId,
                Title: title,
                PaidByParticipantId: paidByParticipantId,
                AmountMinor: amountMinor,
                SplitDefinition: split,
                Date: date.ToUniversalTime().ToString("O")));

        _lastPaidByParticipantId = paidByParticipantId;
        _lastParticipantIds = participantIds.ToArray();

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPaymentAsync(string fromParticipantId, string toParticipantId, long amountMinor, DateTime date)
    {
        var infra = await GetInfraAsync();

        await new AddManualTransferUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.TransferRepository,
            new GuidIdGenerator(),
            new UtcClock()).ExecuteAsync(new AddManualTransferInput(
                GroupId: DefaultGroupId,
                FromParticipantId: fromParticipantId,
                ToParticipantId: toParticipantId,
                AmountMinor: amountMinor,
                Date: date.ToUniversalTime().ToString("O"),
                Note: "Recorded in app"));

        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<(IReadOnlyList<BalanceModel> Balances, SettlementPlanModel Settlement)> GetSettlementAsync(SettlementMode mode)
    {
        var infra = await GetInfraAsync();

        IReadOnlyList<BalanceModel> balances = mode == SettlementMode.Participant
            ? await new GetBalancesByParticipantUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.ExpenseRepository, infra.TransferRepository)
                .ExecuteAsync(DefaultGroupId)
            : await new GetBalancesByEconomicUnitOwnerUseCase(infra.GroupRepository, infra.ParticipantRepository, infra.EconomicUnitRepository, infra.ExpenseRepository, infra.TransferRepository)
                .ExecuteAsync(DefaultGroupId);

        var settlement = await new GetSettlementPlanUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(DefaultGroupId, mode);

        return (balances, settlement);
    }

    public async ValueTask DisposeAsync()
    {
        if (_infra is not null)
        {
            _infra.Dispose();
            _infra = null;
        }

        _gate.Dispose();
        await Task.CompletedTask;
    }

    private async Task<InfraLocalSqlite> GetInfraAsync()
    {
        await InitializeAsync();
        return _infra ?? throw new InvalidOperationException("Infrastructure not initialized.");
    }

    private async Task EnsureSeedDataAsync()
    {
        var infra = _infra ?? throw new InvalidOperationException("Infrastructure not initialized.");
        var existing = await infra.GroupRepository.GetByIdAsync(DefaultGroupId, CancellationToken.None);
        if (existing is not null)
        {
            return;
        }

        await infra.GroupRepository.SaveGroupAsync(new Group(DefaultGroupId, "USD", false), CancellationToken.None);

        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u1", DefaultGroupId, "p1", "Household A"), CancellationToken.None);
        await infra.EconomicUnitRepository.SaveEconomicUnitAsync(new EconomicUnit("u2", DefaultGroupId, "p2", "Household B"), CancellationToken.None);

        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p1", DefaultGroupId, "u1", "Alex", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p2", DefaultGroupId, "u2", "Blair", ConsumptionCategory.Full), CancellationToken.None);
        await infra.ParticipantRepository.SaveParticipantAsync(new Participant("p3", DefaultGroupId, "u2", "Casey", ConsumptionCategory.Half), CancellationToken.None);

        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e1",
            DefaultGroupId,
            "Dinner",
            "p1",
            900,
            DateTimeOffset.UtcNow.AddDays(-2).ToString("O"),
            new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { "p1", "p2", "p3" }, RemainderMode.Equal)
            }),
            null), CancellationToken.None);

        await infra.ExpenseRepository.SaveAsync(new Expense(
            "e2",
            DefaultGroupId,
            "Groceries",
            "p2",
            600,
            DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
            new SplitDefinition(new SplitComponent[]
            {
                new FixedSplitComponent(new Dictionary<string, long> { ["p1"] = 100 }),
                new RemainderSplitComponent(new[] { "p2", "p3" }, RemainderMode.Equal)
            }),
            "Weekly staples"), CancellationToken.None);
    }

    private sealed class GuidIdGenerator : LuSplit.Application.Ports.IIdGenerator
    {
        public string NextId() => Guid.NewGuid().ToString("N")[..12];
    }

    private sealed class UtcClock : LuSplit.Application.Ports.IClock
    {
        public string NowIso() => DateTimeOffset.UtcNow.ToString("O");
    }
}

public sealed record EventDraftDefaults(string? PaidByParticipantId, IReadOnlyList<string> ParticipantIds);
