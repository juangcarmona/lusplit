namespace LuSplit.Application.Groups.Models;

public sealed record ParticipantModel(
    string Id,
    string GroupId,
    string EconomicUnitId,
    string Name,
    string ConsumptionCategory,
    string? CustomConsumptionWeight);