namespace LuSplit.Application.Payments.Models;

public sealed record TransferModel(
    string Id,
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string Date,
    string Type,
    string? Note);