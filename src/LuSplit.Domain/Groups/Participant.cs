namespace LuSplit.Domain.Groups;

public sealed record Participant(
    string Id,
    string GroupId,
    string EconomicUnitId,
    string Name,
    ConsumptionCategory ConsumptionCategory,
    string? CustomConsumptionWeight = null);
