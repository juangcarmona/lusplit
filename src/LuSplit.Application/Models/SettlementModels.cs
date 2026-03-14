namespace LuSplit.Application.Models;

public enum SettlementMode
{
    Participant,
    EconomicUnitOwner
}

public sealed record SettlementTransferModel(string FromParticipantId, string ToParticipantId, long AmountMinor);

public sealed record SettlementPlanModel(SettlementMode Mode, IReadOnlyList<SettlementTransferModel> Transfers);
