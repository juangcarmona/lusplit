using LuSplit.Domain.Split;

namespace LuSplit.Application.Commands;

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
