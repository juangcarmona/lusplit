namespace LuSplit.Application.Groups.Models;

public sealed record GroupSummaryModel(
    string GroupId,
    int ParticipantCount,
    int EconomicUnitCount,
    int ExpenseCount,
    int TransferCount);
