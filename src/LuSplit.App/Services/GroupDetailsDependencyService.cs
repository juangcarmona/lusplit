using LuSplit.App.Pages;
using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Services;

public static class GroupDetailsDependencyService
{
    public static IReadOnlyList<GroupPersonEditorViewModel> RebuildDraftPeopleRelationships(IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        if (people.Count == 0)
        {
            return Array.Empty<GroupPersonEditorViewModel>();
        }

        var ownerByResponsibility = people
            .Where(person => !string.IsNullOrWhiteSpace(person.HouseholdName))
            .GroupBy(person => person.HouseholdName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return people
            .Select(person =>
            {
                if (string.IsNullOrWhiteSpace(person.HouseholdName))
                {
                    return person with
                    {
                        RelationshipText = AppResources.GroupDetails_DependencyIndependent,
                        IsDependent = false,
                        IsOwner = true
                    };
                }

                var owner = ownerByResponsibility.TryGetValue(person.HouseholdName, out var responsibilityOwner)
                    ? responsibilityOwner
                    : null;
                if (owner is null || ReferenceEquals(owner, person))
                {
                    var dependentNames = people
                        .Where(candidate =>
                            !ReferenceEquals(candidate, person)
                            && string.Equals(candidate.HouseholdName, person.HouseholdName, StringComparison.OrdinalIgnoreCase))
                        .Select(candidate => candidate.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var dependents = dependentNames.Length;
                    return person with
                    {
                        RelationshipText = dependents <= 0
                            ? AppResources.GroupDetails_DependencyIndependent
                            : string.Format(AppResources.GroupDetails_DependencyResponsibleForFormat, string.Join(", ", dependentNames)),
                        IsDependent = false,
                        IsOwner = true
                    };
                }

                return person with
                {
                    RelationshipText = string.Format(AppResources.GroupDetails_DependencyDependsOnFormat, owner.Name),
                    IsDependent = true,
                    IsOwner = false
                };
            })
            .ToArray();
    }

    public static IReadOnlyList<string> BuildResponsibleParticipantOptions(IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        return people
            .Where(person => !person.IsDependent)
            .Select(person => person.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsEligibleResponsibleParticipantName(string name, IReadOnlyCollection<string> responsibleParticipantOptions)
        => new HashSet<string>(responsibleParticipantOptions, StringComparer.OrdinalIgnoreCase).Contains(name);

    public static bool CanEditDependency(GroupPersonEditorViewModel person, IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        if (!person.IsOwner || string.IsNullOrWhiteSpace(person.HouseholdName))
        {
            return true;
        }

        return !people.Any(candidate =>
            candidate.IsDependent
            && string.Equals(candidate.HouseholdName, person.HouseholdName, StringComparison.OrdinalIgnoreCase));
    }

    public static string? ResolveCurrentResponsibleName(GroupPersonEditorViewModel person, IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        if (string.IsNullOrWhiteSpace(person.HouseholdName) || person.IsOwner)
        {
            return null;
        }

        var owner = people.FirstOrDefault(candidate =>
            candidate.IsOwner
            && string.Equals(candidate.HouseholdName, person.HouseholdName, StringComparison.OrdinalIgnoreCase));
        return owner?.Name;
    }

    public static IReadOnlyList<string> BuildEditResponsibleParticipantOptions(
        GroupPersonEditorViewModel selectedPerson,
        IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        return people
            .Where(person => !person.IsDependent
                             && !string.Equals(person.ParticipantId, selectedPerson.ParticipantId, StringComparison.Ordinal))
            .Select(person => person.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ResolveCurrentResponsibleParticipantId(
        GroupPersonEditorViewModel selectedPerson,
        IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        if (string.IsNullOrWhiteSpace(selectedPerson.HouseholdName))
        {
            return null;
        }

        var owner = people.FirstOrDefault(candidate =>
            candidate.IsOwner
            && string.Equals(candidate.HouseholdName, selectedPerson.HouseholdName, StringComparison.OrdinalIgnoreCase));
        return owner?.ParticipantId;
    }

    public static string? ResolveEditResponsibleParticipantId(
        GroupPersonEditorViewModel selectedPerson,
        string? editSelectedResponsibleParticipantName,
        bool canChangeSelectedPersonDependency,
        IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        if (!canChangeSelectedPersonDependency)
        {
            return ResolveCurrentResponsibleParticipantId(selectedPerson, people);
        }

        if (string.IsNullOrWhiteSpace(editSelectedResponsibleParticipantName))
        {
            return null;
        }

        var responsible = people.FirstOrDefault(person =>
            !person.IsDependent
            && !string.Equals(person.ParticipantId, selectedPerson.ParticipantId, StringComparison.Ordinal)
            && string.Equals(person.Name, editSelectedResponsibleParticipantName, StringComparison.OrdinalIgnoreCase));
        return responsible?.ParticipantId;
    }

    public static bool IsCircularSelection(
        GroupPersonEditorViewModel selectedPerson,
        string selectedResponsibleName,
        IReadOnlyList<GroupPersonEditorViewModel> people)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selectedPerson.Name };
        var cursor = selectedResponsibleName;
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!visited.Add(cursor))
            {
                return true;
            }

            var cursorPerson = people.FirstOrDefault(person => string.Equals(person.Name, cursor, StringComparison.OrdinalIgnoreCase));
            if (cursorPerson is null || string.IsNullOrWhiteSpace(cursorPerson.HouseholdName) || cursorPerson.IsOwner)
            {
                break;
            }

            cursor = ResolveCurrentResponsibleName(cursorPerson, people);
        }

        return false;
    }
}
