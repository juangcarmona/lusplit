using LuSplit.Domain.Expenses;

namespace LuSplit.Application.Expenses.Models;

public sealed record ExpenseModel(
    string Id,
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    string Date,
    SplitDefinition SplitDefinition,
    string? Notes);
