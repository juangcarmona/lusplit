namespace LuSplit.Domain.Groups;

public sealed record EconomicUnit(
    string Id,
    string GroupId,
    string OwnerParticipantId,
    string? Name = null);
