using System.Collections.ObjectModel;
using System.Windows.Input;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ParticipantsEditorView : ContentView
{
    // ── BindableProperties ───────────────────────────────────────────────────

    public static readonly BindableProperty ParticipantsProperty =
        BindableProperty.Create(nameof(Participants), typeof(ObservableCollection<ParticipantDraftViewModel>),
            typeof(ParticipantsEditorView), null);

    public static readonly BindableProperty CanEditProperty =
        BindableProperty.Create(nameof(CanEdit), typeof(bool), typeof(ParticipantsEditorView), true);

    public static readonly BindableProperty HostPageProperty =
        BindableProperty.Create(nameof(HostPage), typeof(Page), typeof(ParticipantsEditorView), null);

    public ObservableCollection<ParticipantDraftViewModel>? Participants
    {
        get => (ObservableCollection<ParticipantDraftViewModel>?)GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }

    public bool CanEdit
    {
        get => (bool)GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public Page? HostPage
    {
        get => (Page?)GetValue(HostPageProperty);
        set => SetValue(HostPageProperty, value);
    }

    // ── Local state ──────────────────────────────────────────────────────────

    private string _newParticipantName = string.Empty;
    public string NewParticipantName
    {
        get => _newParticipantName;
        set { _newParticipantName = value; OnPropertyChanged(); }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusText)); }
    }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(_statusText);

    // ICommand used by Entry ReturnCommand so Enter key triggers Add
    public ICommand AddCommand => new Command(_ => TryAddParticipant());

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired after name validation passes. Host is responsible for appending the participant to the collection.</summary>
    public event EventHandler<string>? AddParticipantRequested;

    /// <summary>Fired after dependents are cleared. Host is responsible for removing the item from the collection.</summary>
    public event EventHandler<ParticipantDraftViewModel>? RemoveParticipantRequested;

    /// <summary>Fired after the VM's <see cref="ParticipantDraftViewModel.DependsOn"/> has already been updated. Host
    /// persists the change (API call) for edit-mode; create-mode ignores this event.</summary>
    public event EventHandler<ParticipantDraftViewModel>? DependencyChanged;

    // ── Constructor ──────────────────────────────────────────────────────────

    public ParticipantsEditorView()
    {
        InitializeComponent();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnAddClicked(object? sender, EventArgs e) => TryAddParticipant();

    private void TryAddParticipant()
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

    private async void OnDependencyClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantName }) return;

        var participants = Participants;
        if (participants is null || HostPage is null) return;

        var participant = participants.FirstOrDefault(p => string.Equals(p.Name, participantName, StringComparison.Ordinal));
        if (participant is null) return;

        // Only independent participants (no DependsOn) can be selected as responsible
        var eligibleResponsibles = participants
            .Where(p => !string.Equals(p.Name, participant.Name, StringComparison.Ordinal)
                     && string.IsNullOrWhiteSpace(p.DependsOn))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var options = new[] { AppResources.GroupDetails_DependencyIndependent }
            .Concat(eligibleResponsibles)
            .ToArray();

        var selected = await HostPage.DisplayActionSheetAsync(
            AppResources.GroupDetails_DependsOnLabel,
            AppResources.Common_Cancel,
            null,
            options);

        if (string.IsNullOrEmpty(selected) || string.Equals(selected, AppResources.Common_Cancel, StringComparison.Ordinal))
            return;

        if (string.Equals(selected, AppResources.GroupDetails_DependencyIndependent, StringComparison.Ordinal))
        {
            participant.DependsOn = null;
        }
        else
        {
            // Guard: selected must still be in the eligible list (race condition safety)
            var isEligible = eligibleResponsibles.Contains(selected, StringComparer.OrdinalIgnoreCase);
            if (!isEligible)
            {
                StatusText = AppResources.Validation_ResponsiblePersonNotFound;
                return;
            }

            if (GroupDetailsDependencyService.WouldCreateCycleDraft(participant.Name, selected, participants))
            {
                StatusText = AppResources.Validation_CircularDependency;
                return;
            }

            participant.DependsOn = selected;
        }

        participant.Notify();
        StatusText = string.Empty;
        DependencyChanged?.Invoke(this, participant);
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ParticipantDraftViewModel target }) return;

        var participants = Participants;
        if (participants is null) return;

        // Clear dependents before removal to maintain a valid graph
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
}
