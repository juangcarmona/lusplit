using LuSplit.Domain.Errors;

namespace LuSplit.Domain.Settlement;

public sealed record SettlementTransfer(string FromParticipantId, string ToParticipantId, long AmountMinor);

public static class SettlementPlanner
{
    public static IReadOnlyList<SettlementTransfer> PlanSettlement(IReadOnlyDictionary<string, long> balances)
    {
        var sum = balances.Values.Sum();
        if (sum != 0)
        {
            throw new DomainInvariantException($"Settlement invariant violated: sum={sum}");
        }

        var creditors = balances
            .Where(entry => entry.Value > 0)
            .Select(entry => new Bucket(entry.Key, entry.Value))
            .OrderBy(entry => entry.ParticipantId, StringComparer.Ordinal)
            .ToList();

        var debtors = balances
            .Where(entry => entry.Value < 0)
            .Select(entry => new Bucket(entry.Key, -entry.Value))
            .OrderBy(entry => entry.ParticipantId, StringComparer.Ordinal)
            .ToList();

        var transfers = new List<SettlementTransfer>();

        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Count && debtorIndex < debtors.Count)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var amountMinor = Math.Min(creditor.AmountMinor, debtor.AmountMinor);

            if (amountMinor > 0)
            {
                transfers.Add(new SettlementTransfer(debtor.ParticipantId, creditor.ParticipantId, amountMinor));
            }

            creditor.AmountMinor -= amountMinor;
            debtor.AmountMinor -= amountMinor;

            if (creditor.AmountMinor == 0)
            {
                creditorIndex += 1;
            }

            if (debtor.AmountMinor == 0)
            {
                debtorIndex += 1;
            }
        }

        return transfers;
    }

    private sealed class Bucket
    {
        public Bucket(string participantId, long amountMinor)
        {
            ParticipantId = participantId;
            AmountMinor = amountMinor;
        }

        public string ParticipantId { get; }

        public long AmountMinor { get; set; }
    }
}
