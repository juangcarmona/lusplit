namespace LuSplit.Application.Models;

public sealed record GroupSummaryModel(
    string GroupId,
    int ParticipantCount,
    int EconomicUnitCount,
    int ExpenseCount,
    int TransferCount);

public sealed record GroupOverviewModel(
    GroupModel Group,
    GroupSummaryModel Summary,
    IReadOnlyList<ParticipantModel> Participants,
    IReadOnlyList<EconomicUnitModel> EconomicUnits,
    IReadOnlyList<ExpenseModel> Expenses,
    IReadOnlyList<TransferModel> Transfers,
    IReadOnlyList<BalanceModel> BalancesByParticipant,
    IReadOnlyList<BalanceModel> BalancesByEconomicUnitOwner,
    SettlementPlanModel SettlementByParticipant,
    SettlementPlanModel SettlementByEconomicUnitOwner);
