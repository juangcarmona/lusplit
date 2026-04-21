using System.Text.Json;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Payments.Ports;
using LuSplit.Application.Sync;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Payments;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Tests.Sync;

public sealed class OperationApplicatorTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly InMemoryQueryRepositories _repos = new();
    private readonly OperationApplicator _applicator;

    public OperationApplicatorTests()
    {
        _applicator = new OperationApplicator(_repos, _repos, _repos);
    }

    private static byte[] Json<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);

    private Operation MakeOp(OperationType type, byte[] payload, string? entityId = null, string? groupId = "g1")
        => new(
            Guid.NewGuid().ToString(),
            groupId!,
            "device1",
            "user1",
            DateTimeOffset.UtcNow.ToString("o"),
            type,
            entityId ?? Guid.NewGuid().ToString(),
            payload,
            1,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddExpense_CreatesExpenseInRepository()
    {
        var payload = new AddExpensePayload(
            "exp1",
            "Dinner",
            100m,
            "USD",
            "p1",
            DateTimeOffset.UtcNow,
            [new SplitLinePayload("p1", 60m), new SplitLinePayload("p2", 40m)]);

        var op = MakeOp(OperationType.AddExpense, Json(payload), "exp1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Single(_repos.Expenses);
        Assert.Equal("exp1", _repos.Expenses[0].Id);
        Assert.Equal("Dinner", _repos.Expenses[0].Title);
    }

    [Fact]
    public async Task DeleteExpense_RemovesExpenseFromRepository()
    {
        // Seed an expense first
        _repos.Expenses.Add(new Expense("exp1", "g1", "Lunch", "p1", 5000, "2024-01-01", new SplitDefinition([]), null));

        var payload = new DeleteExpensePayload("exp1");
        var op = MakeOp(OperationType.DeleteExpense, Json(payload), "exp1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Empty(_repos.Expenses);
    }

    [Fact]
    public async Task AddParticipant_CreatesParticipantInRepository()
    {
        var payload = new AddParticipantPayload("p1", "Alice");
        var op = MakeOp(OperationType.AddParticipant, Json(payload), "p1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Single(_repos.Participants);
        Assert.Equal("Alice", _repos.Participants[0].Name);
    }

    [Fact]
    public async Task RecordPayment_CreatesTransferInRepository()
    {
        var payload = new RecordPaymentPayload("pay1", "p1", "p2", 50m, DateTimeOffset.UtcNow);
        var op = MakeOp(OperationType.RecordPayment, Json(payload), "pay1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Single(_repos.Transfers);
        Assert.Equal("pay1", _repos.Transfers[0].Id);
    }

    [Fact]
    public async Task DeletePayment_RemovesTransferFromRepository()
    {
        _repos.Transfers.Add(new Transfer("pay1", "g1", "p1", "p2", 5000, "2024-01-01", TransferType.Manual, null));

        var payload = new DeletePaymentPayload("pay1");
        var op = MakeOp(OperationType.DeletePayment, Json(payload), "pay1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Empty(_repos.Transfers);
    }

    [Fact]
    public async Task DeleteTransfer_RemovesTransferFromRepository()
    {
        _repos.Transfers.Add(new Transfer("tr1", "g1", "p1", "p2", 5000, "2024-01-01", TransferType.Generated, null));

        var payload = new DeleteTransferPayload("tr1");
        var op = MakeOp(OperationType.DeleteTransfer, Json(payload), "tr1");
        await _applicator.ApplyAsync(op, CancellationToken.None);

        Assert.Empty(_repos.Transfers);
    }
}
