using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Features.Expenses.Shared;

public sealed record DependencySelectionArgs(string ParticipantName, IReadOnlyList<string> Options);

public sealed partial class ParticipantsEditorViewModel : ObservableObject
{
    [ObservableProperty] private string _newParticipantName = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _canEdit = true;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public ObservableCollection<ParticipantDraftViewModel>? Participants { get; set; }

    public event EventHandler<string>? AddParticipantRequested;
    public event EventHandler<ParticipantDraftViewModel>? RemoveParticipantRequested;
    public event EventHandler<ParticipantDraftViewModel>? DependencyChanged;

    /// <summary>
    /// Fired when the user taps a dependency chip. Code-behind shows the action sheet
    /// and calls <see cref="ApplyDependencySelection"/> with the user's choice.
    /// </summary>
    public event EventHandler<DependencySelectionArgs>? DependencySelectionRequested;

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatusText));

    [RelayCommand]
    private void Add()
    {
        var name = NewParticipantName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = AppResources.Validation_PersonNameRequired;
            return;
        }

        if (Participants?.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) == true)
        {
            StatusText = AppResources.Validation_PersonNameMustBeUnique;
            return;
        }

        NewParticipantName = string.Empty;
        StatusText = string.Empty;
        AddParticipantRequested?.Invoke(this, name);
    }

    [RelayCommand]
    private void Remove(ParticipantDraftViewModel target)
    {
        var participants = Participants;
        if (participants is null) return;

        // Clear dependents before raising removal so the graph stays valid.
        foreach (var p in participants)
        {
            if (string.Equals(p.DependsOn, target.Name, StringComparison.OrdinalIgnoreCase))
            {
                p.DependsOn = null;
                p.Notify();
            }
        }

        RemoveParticipantRequested?.Invoke(this, target);
    }

    [RelayCommand]
    private void RequestDependencySelection(string participantName)
    {
        var participants = Participants;
        if (participants is null) return;

        var participant = participants.FirstOrDefault(p =>
            string.Equals(p.Name, participantName, StringComparison.Ordinal));
        if (participant is null) return;

        var eligible = BuildEligibleResponsibles(participant, participants);
        var options = (IReadOnlyList<string>)new[] { AppResources.GroupDetails_DependencyIndependent }
            .Concat(eligible)
            .ToArray();

        DependencySelectionRequested?.Invoke(this, new DependencySelectionArgs(participantName, options));
    }

    /// <summary>
    /// Applies a dependency selection made by the user in the action sheet.
    /// Called by code-behind after <see cref="DependencySelectionRequested"/> has been
    /// fulfilled and the cancel / dismiss paths have been filtered out.
    /// </summary>
    public void ApplyDependencySelection(string participantName, string selected)
    {
        var participants = Participants;
        if (participants is null) return;

        var participant = participants.FirstOrDefault(p =>
            string.Equals(p.Name, participantName, StringComparison.Ordinal));
        if (participant is null) return;

        if (string.Equals(selected, AppResources.GroupDetails_DependencyIndependent, StringComparison.Ordinal))
        {
            participant.DependsOn = null;
        }
        else
        {
            var eligible = BuildEligibleResponsibles(participant, participants);

            if (!eligible.Contains(selected, StringComparer.OrdinalIgnoreCase))
            {
                StatusText = AppResources.Validation_ResponsiblePersonNotFound;
                return;
            }

            if (WouldCreateCycleDraft(participant.Name, selected, participants))
            {
                StatusText = AppResources.Validation_CircularDependency;
                return;
            }

            participant.DependsOn = selected;
        }

        participant.Notify();
        ReorderParticipants();
        StatusText = string.Empty;
        DependencyChanged?.Invoke(this, participant);
    }

    public void ReorderParticipants()
    {
        var participants = Participants;
        if (participants is null || participants.Count <= 1) return;

        var source = participants.ToList();
        var roots = source.Where(p => string.IsNullOrWhiteSpace(p.DependsOn)).ToList();
        var ordered = new List<ParticipantDraftViewModel>();

        foreach (var root in roots)
        {
            ordered.Add(root);
            ordered.AddRange(source.Where(p =>
                string.Equals(p.DependsOn, root.Name, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var orphan in source.Except(ordered))
            ordered.Add(orphan);

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = participants.IndexOf(ordered[i]);
            if (current != i) participants.Move(current, i);
        }
    }

    private static IReadOnlyList<string> BuildEligibleResponsibles(
        ParticipantDraftViewModel participant,
        IEnumerable<ParticipantDraftViewModel> participants)
    {
        return participants
            .Where(p => !string.Equals(p.Name, participant.Name, StringComparison.Ordinal)
                     && string.IsNullOrWhiteSpace(p.DependsOn))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool WouldCreateCycleDraft(
        string participantName,
        string? selectedResponsible,
        IEnumerable<ParticipantDraftViewModel> participants)
    {
        if (string.IsNullOrWhiteSpace(selectedResponsible)) return false;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { participantName };
        var cursor = selectedResponsible;
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!visited.Add(cursor)) return true;
            cursor = participants
                .FirstOrDefault(p => string.Equals(p.Name, cursor, StringComparison.OrdinalIgnoreCase))
                ?.DependsOn;
        }

        return false;
    }
}
