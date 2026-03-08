using LuSplit.Domain.Split;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Commands;

public sealed record CreateGroupInput(string Currency);

public sealed record CreateEconomicUnitInput(string GroupId, string OwnerParticipantId, string? Name = null);

public sealed record CreateParticipantInput(
    string GroupId,
    string EconomicUnitId,
    string Name,
    ConsumptionCategory ConsumptionCategory,
    string? CustomConsumptionWeight = null);

public sealed record AddManualTransferInput(
    string GroupId,
    string FromParticipantId,
    string ToParticipantId,
    long AmountMinor,
    string? Date = null,
    string? Note = null);

public sealed record AddExpenseInput(
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    SplitDefinition SplitDefinition,
    string? Date = null,
    string? Notes = null);

public sealed record EditExpenseInput(
    string GroupId,
    string ExpenseId,
    string? Title = null,
    string? PaidByParticipantId = null,
    long? AmountMinor = null,
    SplitDefinition? SplitDefinition = null,
    string? Date = null,
    string? Notes = null);

public sealed record DeleteExpenseInput(string GroupId, string ExpenseId);

public sealed record CloseGroupInput(string GroupId);
