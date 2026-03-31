namespace LuSplit.Domain.Expenses;

public sealed record Expense(
    string Id,
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    string Date,
    SplitDefinition SplitDefinition,
    string? Notes = null);
