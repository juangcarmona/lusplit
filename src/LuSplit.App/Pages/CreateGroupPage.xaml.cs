using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Domain.Entities;

namespace LuSplit.App.Pages;

public partial class CreateGroupPage : ContentPage
{
    private readonly AppDataService _dataService;
    private int _step = 1;

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };
    public ObservableCollection<ParticipantDraftViewModel> Participants { get; } = new();

    public string GroupName { get; set; } = string.Empty;
    public string? SelectedCurrency { get; set; } = AppPreferences.GetPreferredCurrency();
    public string StatusText { get; set; } = string.Empty;

    public bool IsStep1 => _step == 1;
    public bool IsStep2 => _step == 2;

    public CreateGroupPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    private void OnContinueClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            StatusText = AppResources.Validation_GroupNameRequired;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCurrency))
        {
            StatusText = AppResources.Validation_SelectCurrency;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        _step = 2;
        EnsureCurrentUserParticipant();
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));
    }

    // ── ParticipantsEditorView event handlers ──────────────────────────────

    private void OnParticipantAddRequested(object? sender, string name)
    {
        Participants.Add(new ParticipantDraftViewModel(name));
    }

    private void OnParticipantRemoveRequested(object? sender, ParticipantDraftViewModel participant)
    {
        Participants.Remove(participant);
    }

    // Dependency changes are fully handled inside ParticipantsEditorView (no persistence needed in create mode).
    private void OnDependencyChanged(object? sender, ParticipantDraftViewModel participant) { }

    // ── Create handler ────────────────────────────────────────────────────

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (Participants.Count == 0)
        {
            StatusText = AppResources.Validation_AddAtLeastOnePerson;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        try
        {
            EnsureCurrentUserParticipant();
            var orderedParticipants = Participants.ToArray();

            var drafts = orderedParticipants.Select(person => new GroupDraftMember(
                person.Name,
                // householdName: own name if independent, responsible's name if dependent
                string.IsNullOrWhiteSpace(person.DependsOn) ? person.Name : person.DependsOn,
                ConsumptionCategory.Full,
                null)).ToArray();

            await _dataService.CreateGroupAsync(GroupName.Trim(), SelectedCurrency!, drafts);
            await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void EnsureCurrentUserParticipant()
    {
        var preferredName = UserProfilePreferences.GetPreferredName();
        var localizedMe = AppResources.Mapper_Me;
        var fallbackName = string.IsNullOrWhiteSpace(localizedMe)
            ? "Me"
            : localizedMe.Length == 1
                ? char.ToUpperInvariant(localizedMe[0]).ToString()
                : char.ToUpperInvariant(localizedMe[0]) + localizedMe[1..];
        var participantName = string.IsNullOrWhiteSpace(preferredName) ? fallbackName : preferredName;

        var existing = Participants
            .Select((p, i) => (p, i))
            .FirstOrDefault(x => string.Equals(x.p.Name, participantName, StringComparison.OrdinalIgnoreCase));

        if (existing.p is null)
        {
            // Creator always goes first and cannot be removed
            Participants.Insert(0, new ParticipantDraftViewModel(participantName, canRemove: false));
            return;
        }

        // Promote to first position if needed, ensuring CanRemove=false
        if (existing.i > 0 || existing.p.CanRemove)
        {
            var promoted = new ParticipantDraftViewModel(existing.p.Name, canRemove: false)
            {
                DependsOn = existing.p.DependsOn
            };
            Participants.RemoveAt(existing.i);
            Participants.Insert(0, promoted);
        }
    }
}
