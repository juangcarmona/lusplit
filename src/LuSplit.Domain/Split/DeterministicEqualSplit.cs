using LuSplit.Domain.Errors;
using LuSplit.Domain.Money;

namespace LuSplit.Domain.Split;

public static class DeterministicEqualSplit
{
    public static IReadOnlyDictionary<string, long> Evaluate(IEnumerable<string> participantIds, MoneyAmount amount)
    {
        var orderedDistinct = participantIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (orderedDistinct.Distinct(StringComparer.Ordinal).Count() != orderedDistinct.Length)
        {
            throw new DomainInvariantException("Participant IDs must be unique.");
        }

        if (orderedDistinct.Length == 0)
        {
            if (amount.MinorUnits == 0)
            {
                return new Dictionary<string, long>();
            }

            throw new DomainInvariantException("Non-zero amounts require at least one participant.");
        }

        var baseShare = amount.MinorUnits / orderedDistinct.Length;
        var remainder = amount.MinorUnits % orderedDistinct.Length;

        var shares = orderedDistinct.ToDictionary(id => id, _ => baseShare, StringComparer.Ordinal);

        // Allocate remainder by deterministic lexical order for parity with TS ordering assumptions.
        for (var i = 0; i < remainder; i++)
        {
            shares[orderedDistinct[i]] += 1;
        }

        return shares;
    }
}
