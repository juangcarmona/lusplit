namespace LuSplit.Domain.Entities;

public enum ConsumptionCategory
{
    Full,
    Half,
    Custom
}

public sealed record Group(string Id, string Currency, bool Closed);

public sealed record Participant(
    string Id,
    string GroupId,
    string EconomicUnitId,
    string Name,
    ConsumptionCategory ConsumptionCategory,
    string? CustomConsumptionWeight = null);

public sealed record EconomicUnit(
    string Id,
    string GroupId,
    string OwnerParticipantId,
    string? Name = null);

public sealed record Expense(
    string Id,
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    string Date,
    Split.SplitDefinition SplitDefinition,
    string? Notes = null);

public enum TransferType
{
    Generated,
    Manual
}

public sealed record Transfer(
    string Id,
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string Date,
    TransferType Type,
    string? Note = null);
