namespace LuSplit.Infrastructure.Snapshot;

public sealed record GroupSnapshotV1(
    int Version,
    SnapshotGroup Group,
    IReadOnlyList<SnapshotParticipant> Participants,
    IReadOnlyList<SnapshotEconomicUnit> EconomicUnits,
    IReadOnlyList<SnapshotExpense> Expenses,
    IReadOnlyList<SnapshotTransfer> Transfers);

public sealed record SnapshotGroup(string Id, string Currency, bool Closed);

public sealed record SnapshotParticipant(
    string Id,
    string GroupId,
    string EconomicUnitId,
    string Name,
    string ConsumptionCategory,
    string? CustomConsumptionWeight = null);

public sealed record SnapshotEconomicUnit(
    string Id,
    string GroupId,
    string OwnerParticipantId,
    string? Name = null);

public sealed record SnapshotExpense(
    string Id,
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    string Date,
    object SplitDefinition,
    string? Notes = null);

public sealed record SnapshotTransfer(
    string Id,
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string Date,
    string Type,
    string? Note = null);
