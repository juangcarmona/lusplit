using LuSplit.Domain.Entities;
using LuSplit.Domain.Errors;
using LuSplit.Domain.Split;

namespace LuSplit.Domain.Balance;

public static class BalanceCalculator
{
    public static IReadOnlyDictionary<string, long> CalculateParticipantBalances(
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<Participant> participants)
    {
        var balances = participants.ToDictionary(participant => participant.Id, _ => 0L, StringComparer.Ordinal);

        foreach (var expense in expenses)
        {
            GroupScopeAssertions.AssertGroupScoped(expense.GroupId, participants);

            if (!balances.ContainsKey(expense.PaidByParticipantId))
            {
                throw new DomainInvariantException($"Unknown payer {expense.PaidByParticipantId}");
            }

            balances[expense.PaidByParticipantId] += expense.AmountMinor;

            var shares = SplitEvaluator.EvaluateSplit(expense, participants);
            foreach (var (participantId, share) in shares)
            {
                balances[participantId] -= share;
            }
        }

        var sum = balances.Values.Sum();
        if (sum != 0)
        {
            throw new DomainInvariantException($"Balance invariant violated: sum={sum}");
        }

        return balances;
    }

    public static IReadOnlyDictionary<string, long> AggregateBalancesByEconomicUnitOwner(
        IReadOnlyDictionary<string, long> balances,
        IReadOnlyList<Participant> participants,
        IReadOnlyList<EconomicUnit> economicUnits)
    {
        if (participants.Count == 0 && economicUnits.Count == 0)
        {
            if (balances.Count == 0)
            {
                return new Dictionary<string, long>(StringComparer.Ordinal);
            }

            throw new DomainInvariantException("Cannot aggregate balances without participants or economic units");
        }

        var scopedGroupId = participants.Count > 0 ? participants[0].GroupId : economicUnits[0].GroupId;
        GroupScopeAssertions.AssertGroupScoped(scopedGroupId, participants, economicUnits);

        var participantsById = participants.ToDictionary(participant => participant.Id, StringComparer.Ordinal);
        var ownerByUnit = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var economicUnit in economicUnits)
        {
            ownerByUnit[economicUnit.Id] = economicUnit.OwnerParticipantId;

            if (!participantsById.TryGetValue(economicUnit.OwnerParticipantId, out var owner))
            {
                throw new DomainInvariantException($"Economic unit owner is not a participant: {economicUnit.OwnerParticipantId}");
            }

            if (!string.Equals(owner.EconomicUnitId, economicUnit.Id, StringComparison.Ordinal))
            {
                throw new DomainInvariantException($"Economic unit owner must belong to its own unit: {economicUnit.Id}");
            }
        }

        var unitByParticipant = participants.ToDictionary(
            participant => participant.Id,
            participant => participant.EconomicUnitId,
            StringComparer.Ordinal);

        var aggregated = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (participantId, balance) in balances)
        {
            if (!unitByParticipant.TryGetValue(participantId, out var economicUnitId))
            {
                throw new DomainInvariantException($"Unknown participant in balances: {participantId}");
            }

            if (!ownerByUnit.TryGetValue(economicUnitId, out var ownerId))
            {
                throw new DomainInvariantException($"Economic unit without owner: {economicUnitId}");
            }

            aggregated[ownerId] = (aggregated.TryGetValue(ownerId, out var current) ? current : 0L) + balance;
        }

        return aggregated;
    }
}
