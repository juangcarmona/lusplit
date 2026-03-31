namespace LuSplit.Domain.Payments;

public sealed record Transfer(
    string Id,
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string Date,
    TransferType Type,
    string? Note = null);
