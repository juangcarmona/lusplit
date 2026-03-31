using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.GroupDetails;

/// <summary>
/// Pure sorting and dependency-resolution logic for the GroupDetails participant list.
/// Extracted from GroupDetailsViewModel to enable unit testing without MAUI dependencies.
/// </summary>
internal static class GroupDetailsParticipantSorter
{
    internal sealed record SortEntry(string Name, string? ParticipantId, string? DependsOn);

    /// <summary>
    /// Returns participants sorted so the preferred user appears first, then alphabetically.
    /// Each dependent member has <see cref="SortEntry.DependsOn"/> set to their owner's name.
    /// </summary>
    internal static IReadOnlyList<SortEntry> Sort(
        IReadOnlyList<GroupMemberModel> members,
        string? preferredName)
    {
        var sorted = members
            .OrderBy(m => string.Equals(m.Name, preferredName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<SortEntry>();
        foreach (var member in sorted)
        {
            string? dependsOn = null;
            if (!member.IsOwner)
            {
                var owner = members.FirstOrDefault(o =>
                    o.IsOwner &&
                    string.Equals(o.HouseholdName, member.HouseholdName, StringComparison.OrdinalIgnoreCase));
                dependsOn = owner?.Name;
            }

            result.Add(new SortEntry(member.Name, member.ParticipantId, dependsOn));
        }

        return result;
    }
}
