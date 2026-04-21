namespace LuSplit.Contracts.Sync.Payloads;

public sealed record AddExpensePayload(
    string ExpenseId,
    string Description,
    decimal Amount,
    string Currency,
    string PaidByParticipantId,
    DateTimeOffset Date,
    IReadOnlyList<SplitLinePayload> Splits);

public sealed record EditExpensePayload(
    string ExpenseId,
    string Description,
    decimal Amount,
    string Currency,
    string PaidByParticipantId,
    DateTimeOffset Date,
    IReadOnlyList<SplitLinePayload> Splits);

public sealed record DeleteExpensePayload(string ExpenseId);

public sealed record SplitLinePayload(string ParticipantId, decimal Amount);

public sealed record AddParticipantPayload(string ParticipantId, string Name);

public sealed record EditParticipantPayload(string ParticipantId, string Name);

public sealed record RecordPaymentPayload(
    string PaymentId,
    string FromParticipantId,
    string ToParticipantId,
    decimal Amount,
    DateTimeOffset Date);

public sealed record EditPaymentPayload(
    string PaymentId,
    string FromParticipantId,
    string ToParticipantId,
    decimal Amount,
    DateTimeOffset Date);

public sealed record DeletePaymentPayload(string PaymentId);

public sealed record AddTransferPayload(
    string TransferId,
    string FromParticipantId,
    string ToParticipantId,
    decimal Amount,
    DateTimeOffset Date);

public sealed record EditTransferPayload(
    string TransferId,
    string FromParticipantId,
    string ToParticipantId,
    decimal Amount,
    DateTimeOffset Date);

public sealed record DeleteTransferPayload(string TransferId);
