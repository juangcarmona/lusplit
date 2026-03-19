using LuSplit.App.Pages;
using LuSplit.App.Resources.Localization;
using LuSplit.Application.Models;

namespace LuSplit.App.Services;

public static class GroupDetailsPeopleService
{
    public static IReadOnlyList<GroupPersonEditorViewModel> BuildPeopleViewModels(IReadOnlyList<GroupMemberModel> members)
    {
        var ownerNameByResponsibility = members
            .Where(member => member.IsOwner)
            .GroupBy(member => member.HouseholdName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        var dependentNamesByResponsibility = members
            .Where(member => !member.IsOwner)
            .GroupBy(member => member.HouseholdName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(member => member.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);

        return members
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(member => new GroupPersonEditorViewModel(
                member.ParticipantId,
                member.Name,
                member.HouseholdName,
                false,
                ResolveRelationshipText(member, ownerNameByResponsibility, dependentNamesByResponsibility),
                ResolveIsDependent(member, dependentNamesByResponsibility),
                member.IsOwner,
                member.ConsumptionCategory,
                member.CustomConsumptionWeight))
            .ToArray();
    }

    private static string ResolveRelationshipText(
        GroupMemberModel member,
        IReadOnlyDictionary<string, string> ownerNameByResponsibility,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependentNamesByResponsibility)
    {
        if (member.IsOwner)
        {
            if (!dependentNamesByResponsibility.TryGetValue(member.HouseholdName, out var dependents) || dependents.Count == 0)
            {
                return AppResources.GroupDetails_DependencyIndependent;
            }

            return string.Format(AppResources.GroupDetails_DependencyResponsibleForFormat, string.Join(", ", dependents));
        }

        if (!ownerNameByResponsibility.TryGetValue(member.HouseholdName, out var ownerName))
        {
            return AppResources.GroupDetails_DependencyIndependent;
        }

        return string.Format(AppResources.GroupDetails_DependencyDependsOnFormat, ownerName);
    }

    private static bool ResolveIsDependent(
        GroupMemberModel member,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependentNamesByResponsibility)
    {
        if (member.IsOwner)
        {
            return false;
        }

        return dependentNamesByResponsibility.TryGetValue(member.HouseholdName, out var dependents)
            && dependents.Contains(member.Name, StringComparer.OrdinalIgnoreCase);
    }
}
