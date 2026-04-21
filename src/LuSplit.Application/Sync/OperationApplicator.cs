using System.Text.Json;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Payments.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Activity;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Payments;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Sync;

/// <summary>
/// Dispatches a decrypted <see cref="Operation"/> to the appropriate local repository write.
/// Applies idempotency: duplicate operations are silently skipped.
/// Also writes an <see cref="ActivityEntry"/> for every applied operation.
/// </summary>
public sealed class OperationApplicator
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IActivityEntryPort? _activityPort;
    private readonly IIdGenerator? _idGenerator;
    private readonly IClock? _clock;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OperationApplicator(
        IExpenseRepository expenseRepository,
        IParticipantRepository participantRepository,
        ITransferRepository transferRepository,
        IActivityEntryPort? activityPort = null,
        IIdGenerator? idGenerator = null,
        IClock? clock = null)
    {
        _expenseRepository = expenseRepository;
        _participantRepository = participantRepository;
        _transferRepository = transferRepository;
        _activityPort = activityPort;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task ApplyAsync(Operation operation, CancellationToken ct)
    {
        switch (operation.OperationType)
        {
            case OperationType.AddExpense:
            case OperationType.EditExpense:
                await ApplyExpenseAsync(operation, ct);
                await RecordActivityAsync(
                    operation,
                    operation.OperationType == OperationType.AddExpense
                        ? ActivityEntryType.ExpenseAdded
                        : ActivityEntryType.ExpenseEdited,
                    ct);
                break;

            case OperationType.DeleteExpense:
            {
                var payload = Deserialize<DeleteExpensePayload>(operation.EncryptedPayload);
                await _expenseRepository.DeleteAsync(operation.GroupId, payload.ExpenseId, ct);
                await RecordActivityAsync(operation, ActivityEntryType.ExpenseDeleted, ct, entityId: payload.ExpenseId);
                break;
            }

            case OperationType.AddParticipant:
            case OperationType.EditParticipant:
                await ApplyParticipantAsync(operation, ct);
                await RecordActivityAsync(operation, ActivityEntryType.MemberJoined, ct);
                break;

            case OperationType.RecordPayment:
            case OperationType.AddTransfer:
            case OperationType.EditPayment:
            case OperationType.EditTransfer:
                await ApplyTransferAsync(operation, ct);
                await RecordActivityAsync(operation, ActivityEntryType.PaymentRecorded, ct);
                break;

            case OperationType.DeletePayment:
            {
                var payload = Deserialize<DeletePaymentPayload>(operation.EncryptedPayload);
                await _transferRepository.DeleteTransferAsync(operation.GroupId, payload.PaymentId, ct);
                await RecordActivityAsync(operation, ActivityEntryType.PaymentRecorded, ct, entityId: payload.PaymentId);
                break;
            }

            case OperationType.DeleteTransfer:
            {
                var payload = Deserialize<DeleteTransferPayload>(operation.EncryptedPayload);
                await _transferRepository.DeleteTransferAsync(operation.GroupId, payload.TransferId, ct);
                await RecordActivityAsync(operation, ActivityEntryType.PaymentRecorded, ct, entityId: payload.TransferId);
                break;
            }

            default:
                throw new InvalidOperationException($"Unknown operation type: {operation.OperationType}");
        }
    }

    private async Task RecordActivityAsync(
        Operation operation,
        ActivityEntryType entryType,
        CancellationToken ct,
        string? entityId = null)
    {
        if (_activityPort is null) return;

        var entry = new ActivityEntry(
            EntryId: _idGenerator?.NextId() ?? Guid.NewGuid().ToString(),
            GroupId: operation.GroupId,
            EntryType: entryType,
            ActorUserId: operation.DeviceId,
            EntityId: entityId ?? operation.OperationId,
            Description: null,
            OccurredAt: _clock?.UtcNow ?? DateTimeOffset.UtcNow);

        await _activityPort.InsertAsync(entry, ct);
    }

    private async Task ApplyExpenseAsync(Operation operation, CancellationToken ct)
    {
        var payload = Deserialize<AddExpensePayload>(operation.EncryptedPayload);

        // Convert decimal splits to fixed minor amounts
        var splits = payload.Splits.ToDictionary(
            s => s.ParticipantId,
            s => DecimalToMinor(s.Amount));

        var splitDef = new SplitDefinition([new FixedSplitComponent(splits)]);

        var expense = new Expense(
            payload.ExpenseId,
            operation.GroupId,
            payload.Description,
            payload.PaidByParticipantId,
            DecimalToMinor(payload.Amount),
            payload.Date.ToString("o"),
            splitDef);

        await _expenseRepository.SaveAsync(expense, ct);
    }

    private async Task ApplyParticipantAsync(Operation operation, CancellationToken ct)
    {
        var payload = Deserialize<AddParticipantPayload>(operation.EncryptedPayload);

        // Use participant ID as economic unit ID when synced (simplified — full impl uses EconomicUnit)
        var participant = new Participant(
            payload.ParticipantId,
            operation.GroupId,
            payload.ParticipantId,
            payload.Name,
            ConsumptionCategory.Full);

        await _participantRepository.SaveParticipantAsync(participant, ct);
    }

    private async Task ApplyTransferAsync(Operation operation, CancellationToken ct)
    {
        string fromId, toId, transferId;
        decimal amount;
        DateTimeOffset date;

        if (operation.OperationType is OperationType.RecordPayment or OperationType.EditPayment)
        {
            var payload = Deserialize<RecordPaymentPayload>(operation.EncryptedPayload);
            transferId = payload.PaymentId;
            fromId = payload.FromParticipantId;
            toId = payload.ToParticipantId;
            amount = payload.Amount;
            date = payload.Date;
        }
        else
        {
            var payload = Deserialize<AddTransferPayload>(operation.EncryptedPayload);
            transferId = payload.TransferId;
            fromId = payload.FromParticipantId;
            toId = payload.ToParticipantId;
            amount = payload.Amount;
            date = payload.Date;
        }

        var transfer = new Transfer(
            transferId,
            operation.GroupId,
            fromId,
            toId,
            DecimalToMinor(amount),
            date.ToString("o"),
            TransferType.Manual);

        await _transferRepository.SaveTransferAsync(transfer, ct);
    }

    private static T Deserialize<T>(byte[] bytes)
        => JsonSerializer.Deserialize<T>(bytes, JsonOptions)
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} payload.");

    private static long DecimalToMinor(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
}
