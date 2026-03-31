using System.Numerics;
using System.Text.RegularExpressions;
using LuSplit.Domain.Shared;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;

namespace LuSplit.Domain.Split;

public static partial class SplitEvaluator
{
    public static IReadOnlyDictionary<string, long> EvaluateSplit(Expense expense, IReadOnlyList<Participant> participants)
    {
        GroupScopeAssertions.AssertGroupScoped(expense.GroupId, participants);

        var participantById = participants.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var shares = participants.ToDictionary(p => p.Id, _ => 0L, StringComparer.Ordinal);
        var remaining = expense.AmountMinor;

        foreach (var component in expense.SplitDefinition.Components)
        {
            switch (component)
            {
                case FixedSplitComponent fixedComponent:
                    remaining = ApplyFixedComponent(remaining, fixedComponent, participantById, shares);
                    break;

                case RemainderSplitComponent remainderComponent:
                    ApplyRemainderComponent(remaining, remainderComponent, participantById, shares);
                    remaining = 0;
                    break;

                default:
                    throw new DomainInvariantException("Unsupported split component type.");
            }
        }

        if (remaining != 0)
        {
            throw new DomainInvariantException($"Split definition did not consume full amount. Remaining={remaining}");
        }

        return shares;
    }

    private static long ApplyFixedComponent(
        long remaining,
        FixedSplitComponent component,
        IReadOnlyDictionary<string, Participant> participantById,
        IDictionary<string, long> shares)
    {
        long assigned = 0;

        foreach (var (participantId, amount) in component.Shares)
        {
            AssertKnownParticipant(participantId, participantById);

            if (amount < 0)
            {
                throw new DomainInvariantException("Fixed share must be >= 0");
            }

            assigned += amount;
            shares[participantId] += amount;
        }

        if (assigned > remaining)
        {
            throw new DomainInvariantException("Fixed shares exceed remaining amount");
        }

        return remaining - assigned;
    }

    private static void ApplyRemainderComponent(
        long remaining,
        RemainderSplitComponent component,
        IReadOnlyDictionary<string, Participant> participantById,
        IDictionary<string, long> shares)
    {
        var sortedParticipantIds = component.Participants
            .OrderBy(participantId => participantId, StringComparer.Ordinal)
            .ToArray();

        AssertUniqueParticipants(sortedParticipantIds);

        foreach (var participantId in sortedParticipantIds)
        {
            AssertKnownParticipant(participantId, participantById);
        }

        IReadOnlyDictionary<string, long> allocations = component.Mode switch
        {
            RemainderMode.Percent => AllocatePercentRemainder(remaining, sortedParticipantIds, component),
            RemainderMode.Equal => AllocateEqualRemainder(remaining, sortedParticipantIds),
            RemainderMode.Weight => AllocateWeightedRemainder(remaining, sortedParticipantIds, component, participantById),
            _ => throw new DomainInvariantException("Unsupported remainder mode.")
        };

        foreach (var (participantId, amount) in allocations)
        {
            shares[participantId] += amount;
        }
    }

    private static IReadOnlyDictionary<string, long> AllocatePercentRemainder(
        long totalMinor,
        IReadOnlyList<string> participantIds,
        RemainderSplitComponent component)
    {
        if (component.Percents is null)
        {
            throw new DomainInvariantException("PERCENT mode requires percents");
        }

        var weightByParticipant = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
        var percentSum = 0;

        foreach (var participantId in participantIds)
        {
            if (!component.Percents.TryGetValue(participantId, out var percent) || percent < 0)
            {
                throw new DomainInvariantException($"Invalid percent for {participantId}");
            }

            percentSum += percent;
            weightByParticipant[participantId] = new BigInteger(percent);
        }

        if (percentSum != 100)
        {
            throw new DomainInvariantException($"Percent sum must be exactly 100, got {percentSum}");
        }

        return AllocateByWeights(totalMinor, participantIds, weightByParticipant);
    }

    private static IReadOnlyDictionary<string, long> AllocateEqualRemainder(long totalMinor, IReadOnlyList<string> participantIds)
    {
        var weights = participantIds.ToDictionary(
            participantId => participantId,
            _ => BigInteger.One,
            StringComparer.Ordinal);

        return AllocateByWeights(totalMinor, participantIds, weights);
    }

    private static IReadOnlyDictionary<string, long> AllocateWeightedRemainder(
        long totalMinor,
        IReadOnlyList<string> participantIds,
        RemainderSplitComponent component,
        IReadOnlyDictionary<string, Participant> participantById)
    {
        var weights = new Dictionary<string, BigInteger>(StringComparer.Ordinal);

        foreach (var participantId in participantIds)
        {
            var participant = participantById[participantId];
            string? explicitWeight = null;
            if (component.Weights is not null)
            {
                component.Weights.TryGetValue(participantId, out explicitWeight);
            }

            weights[participantId] = DeriveWeight(participant, explicitWeight);
        }

        return AllocateByWeights(totalMinor, participantIds, weights);
    }

    private static IReadOnlyDictionary<string, long> AllocateByWeights(
        long totalMinor,
        IReadOnlyList<string> participantIds,
        IReadOnlyDictionary<string, BigInteger> weightByParticipant)
    {
        var allocations = new Dictionary<string, long>(StringComparer.Ordinal);
        var totalWeight = weightByParticipant.Values.Aggregate(BigInteger.Zero, (sum, value) => sum + value);

        if (totalWeight <= 0 && totalMinor != 0)
        {
            throw new DomainInvariantException("Total weight must be > 0");
        }

        long allocated = 0;
        var remainders = new List<(string ParticipantId, BigInteger Remainder)>();

        foreach (var participantId in participantIds)
        {
            var weight = weightByParticipant.TryGetValue(participantId, out var value) ? value : BigInteger.Zero;
            var numerator = new BigInteger(totalMinor) * weight;
            var baseAmount = totalWeight == 0 ? 0L : (long)(numerator / totalWeight);
            var remainder = totalWeight == 0 ? BigInteger.Zero : numerator % totalWeight;

            allocations[participantId] = baseAmount;
            allocated += baseAmount;
            remainders.Add((participantId, remainder));
        }

        var leftover = totalMinor - allocated;

        foreach (var (participantId, _) in remainders
                     .OrderByDescending(item => item.Remainder)
                     .ThenBy(item => item.ParticipantId, StringComparer.Ordinal))
        {
            if (leftover == 0)
            {
                break;
            }

            allocations[participantId] += 1;
            leftover -= 1;
        }

        if (leftover != 0)
        {
            throw new DomainInvariantException("Failed to allocate remainder deterministically");
        }

        return allocations;
    }

    private static BigInteger DeriveWeight(Participant participant, string? explicitWeight)
    {
        if (!string.IsNullOrWhiteSpace(explicitWeight))
        {
            return ParseScaledWeight(explicitWeight);
        }

        return participant.ConsumptionCategory switch
        {
            ConsumptionCategory.Full => ParseScaledWeight("1"),
            ConsumptionCategory.Half => ParseScaledWeight("0.5"),
            ConsumptionCategory.Custom => !string.IsNullOrWhiteSpace(participant.CustomConsumptionWeight)
                ? ParseScaledWeight(participant.CustomConsumptionWeight)
                : throw new DomainInvariantException($"Missing customConsumptionWeight for participant {participant.Id}"),
            _ => throw new DomainInvariantException("Unsupported consumption category.")
        };
    }

    private static BigInteger ParseScaledWeight(string value)
    {
        if (!WeightRegex().IsMatch(value))
        {
            throw new DomainInvariantException($"Invalid weight value: {value}");
        }

        var parts = value.Split('.', 2);
        var integerPart = parts[0];
        var fractionalPart = parts.Length == 2 ? parts[1] : string.Empty;

        if (fractionalPart.Length > 6)
        {
            throw new DomainInvariantException($"Weight precision must be <= 6 decimals: {value}");
        }

        var paddedFraction = fractionalPart.PadRight(6, '0');
        var scaled = BigInteger.Parse(integerPart) * 1_000_000 + BigInteger.Parse(string.IsNullOrEmpty(paddedFraction) ? "0" : paddedFraction);

        if (scaled <= 0)
        {
            throw new DomainInvariantException($"Weight must be > 0: {value}");
        }

        return scaled;
    }

    private static void AssertKnownParticipant(string participantId, IReadOnlyDictionary<string, Participant> participantById)
    {
        if (!participantById.ContainsKey(participantId))
        {
            throw new DomainInvariantException($"Unknown participant {participantId}");
        }
    }

    private static void AssertUniqueParticipants(IReadOnlyList<string> participantIds)
    {
        var unique = new HashSet<string>(participantIds, StringComparer.Ordinal);
        if (unique.Count != participantIds.Count)
        {
            throw new DomainInvariantException("Duplicate participants are not allowed in a remainder component");
        }
    }

    [GeneratedRegex(@"^(0|[1-9]\d*)(\.\d+)?$", RegexOptions.Compiled)]
    private static partial Regex WeightRegex();
}
