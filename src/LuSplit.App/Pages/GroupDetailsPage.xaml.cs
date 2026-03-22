using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class GroupDetailsPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private string? _groupId;
    // Set when navigating from an archived group view (GroupPage) to load that specific group
    // without switching the user's currently selected active group.
    private string? _overrideGroupId;
    private bool _isArchived;

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };
    public ObservableCollection<ParticipantDraftViewModel> Participants { get; } = new();

    public string GroupName { get; set; } = string.Empty;
    public string? SelectedCurrency { get; set; } = "USD";
    public string StatusText { get; set; } = string.Empty;

    public bool IsArchived => _isArchived;

    /// <summary>True when the group is not archived - the only case where edits are possible.</summary>
    public bool CanEdit => !_isArchived;

    /// <summary>Archive button is visible only for active (non-archived) groups.</summary>
    public bool CanArchive => !_isArchived;

    public GroupDetailsPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _overrideGroupId = query.TryGetValue("groupId", out var gid) && !string.IsNullOrWhiteSpace(gid?.ToString())
            ? gid.ToString()
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var details = _overrideGroupId is not null
                ? await _dataService.GetGroupDetailsAsync(_overrideGroupId)
                : await _dataService.GetGroupDetailsAsync();

            _groupId = details.GroupId;
            _isArchived = details.IsArchived;
            GroupName = details.GroupName;
            EnsureCurrencyOption(details.Currency);
            SelectedCurrency = details.Currency;
            Title = details.GroupName;

            var preferredName = UserProfilePreferences.GetPreferredName();

            Participants.Clear();
            var sorted = details.Members
                .OrderBy(m => string.Equals(m.Name, preferredName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var member in sorted)
            {
                string? dependsOn = null;
                if (!member.IsOwner)
                {
                    var owner = details.Members.FirstOrDefault(o =>
                        o.IsOwner &&
                        string.Equals(o.HouseholdName, member.HouseholdName, StringComparison.OrdinalIgnoreCase));
                    dependsOn = owner?.Name;
                }

                Participants.Add(new ParticipantDraftViewModel(member.Name, member.ParticipantId, canRemove: false)
                {
                    DependsOn = dependsOn
                });
            }

            StatusText = string.Empty;
            NotifyAllProperties();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    // ── ParticipantsEditorView event handlers ──────────────────────────────

    private void OnParticipantAddRequested(object? sender, string name)
    {
        _ = AddMemberAsync(name);
    }

    private async Task AddMemberAsync(string name)
    {
        if (_groupId is null) return;
        try
        {
            await _dataService.AddGroupMemberAsync(_groupId, name, null);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnDependencyChanged(object? sender, ParticipantDraftViewModel participant)
    {
        _ = UpdateMemberDependencyAsync(participant);
    }

    private async Task UpdateMemberDependencyAsync(ParticipantDraftViewModel participant)
    {
        if (_groupId is null || participant.ParticipantId is null) return;
        try
        {
            var dependsOnId = string.IsNullOrWhiteSpace(participant.DependsOn)
                ? null
                : Participants
                    .FirstOrDefault(p => string.Equals(p.Name, participant.DependsOn, StringComparison.OrdinalIgnoreCase))
                    ?.ParticipantId;

            await _dataService.UpdateGroupMemberAsync(_groupId, participant.ParticipantId, participant.Name, dependsOnId);
            StatusText = string.Empty;
            OnPropertyChanged(nameof(StatusText));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
            // Revert to server state on error
            await LoadAsync();
        }
    }

    // ── Page action handlers ──────────────────────────────────────────────

    private async void OnSaveClicked(object? sender, EventArgs e)
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

        if (string.IsNullOrWhiteSpace(_groupId))
        {
            StatusText = AppResources.Validation_GroupNotFound;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        try
        {
            await _dataService.UpdateGroupAsync(_groupId, GroupName, SelectedCurrency);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async void OnArchiveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_groupId)) return;

        var confirmed = await DisplayAlertAsync(
            AppResources.GroupDetails_ArchiveConfirmTitle,
            AppResources.GroupDetails_ArchiveConfirmMessage,
            AppResources.GroupDetails_ArchiveConfirmYes,
            AppResources.Common_Cancel);

        if (!confirmed) return;

        try
        {
            await _dataService.ArchiveGroupAsync(_groupId);
            await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_groupId is null) return;

        try
        {
            await GroupExportService.RunExportFlowAsync(this, _dataService, _groupId);
        }
        catch (Exception ex)
        {
            StatusText = string.Format(AppResources.Export_Failed, ex.Message);
            OnPropertyChanged(nameof(StatusText));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void EnsureCurrencyOption(string currency)
    {
        if (!CurrencyOptions.Contains(currency, StringComparer.OrdinalIgnoreCase))
            CurrencyOptions.Add(currency.ToUpperInvariant());
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanArchive));
        OnPropertyChanged(nameof(StatusText));
    }
}
