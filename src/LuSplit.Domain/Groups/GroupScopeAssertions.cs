using LuSplit.Domain.Shared;

namespace LuSplit.Domain.Groups;

public static class GroupScopeAssertions
{
    public static void AssertGroupScoped(
        string groupId,
        IEnumerable<Participant> participants,
        IEnumerable<EconomicUnit>? economicUnits = null)
    {
        foreach (var participant in participants)
        {
            if (!string.Equals(participant.GroupId, groupId, StringComparison.Ordinal))
            {
                throw new DomainInvariantException($"Participant {participant.Id} is not in group {groupId}");
            }
        }

        if (economicUnits is null)
        {
            return;
        }

        foreach (var economicUnit in economicUnits)
        {
            if (!string.Equals(economicUnit.GroupId, groupId, StringComparison.Ordinal))
            {
                throw new DomainInvariantException($"EconomicUnit {economicUnit.Id} is not in group {groupId}");
            }
        }
    }
}
