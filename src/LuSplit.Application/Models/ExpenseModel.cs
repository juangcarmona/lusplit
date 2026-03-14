using LuSplit.Domain.Split;

namespace LuSplit.Application.Models;

public sealed record ExpenseModel(
    string Id,
    string GroupId,
    string Title,
    string PaidByParticipantId,
    long AmountMinor,
    string Date,
    SplitDefinition SplitDefinition,
    string? Notes);
