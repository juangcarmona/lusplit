namespace LuSplit.Application.Models;

public sealed record GroupModel(string Id, string Currency, bool Closed);

public sealed record ParticipantModel(
    string Id,
    string GroupId,
    string EconomicUnitId,
    string Name,
    string ConsumptionCategory,
    string? CustomConsumptionWeight);

public sealed record EconomicUnitModel(string Id, string GroupId, string OwnerParticipantId, string? Name);

public sealed record TransferModel(
    string Id,
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string Date,
    string Type,
    string? Note);
