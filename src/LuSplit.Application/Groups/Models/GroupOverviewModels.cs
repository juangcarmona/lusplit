using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Payments.Models;

namespace LuSplit.Application.Groups.Models;

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
