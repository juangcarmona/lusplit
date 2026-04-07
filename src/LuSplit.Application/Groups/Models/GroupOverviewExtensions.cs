using LuSplit.Application.Payments.Models;

namespace LuSplit.Application.Groups.Models;

/// <summary>
/// Projection helpers that derive presentation-ready views from a
/// <see cref="GroupOverviewModel"/> without duplicating the underlying logic.
/// Both export adapters and the UI mapper consume these to ensure they reflect
/// exactly the same final state.
/// </summary>
public static class GroupOverviewExtensions
{
    /// <summary>
    /// Picks the settlement mode that reflects the group's participant structure.
    /// When dependents exist (fewer economic units than participants), owner mode
    /// aggregates dependent balances under the responsible participant — matching
    /// what the app screens display.
    /// </summary>
    public static SettlementMode ResolveSettlementMode(this GroupOverviewModel overview)
        => overview.EconomicUnits.Count >= overview.Participants.Count
            ? SettlementMode.Participant
            : SettlementMode.EconomicUnitOwner;

    /// <summary>Returns the settlement plan that matches the resolved mode.</summary>
    public static SettlementPlanModel ResolveSettlementPlan(this GroupOverviewModel overview)
        => overview.ResolveSettlementMode() == SettlementMode.Participant
            ? overview.SettlementByParticipant
            : overview.SettlementByEconomicUnitOwner;

    /// <summary>Returns the balances collection that matches the resolved mode.</summary>
    public static IReadOnlyList<BalanceModel> ResolveBalances(this GroupOverviewModel overview)
        => overview.ResolveSettlementMode() == SettlementMode.Participant
            ? overview.BalancesByParticipant
            : overview.BalancesByEconomicUnitOwner;
}
