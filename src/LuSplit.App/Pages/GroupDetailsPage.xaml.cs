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

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public ObservableCollection<ParticipantDraftViewModel> Participants { get; } = new();

    public string GroupName { get; set; } = string.Empty;
    public CurrencyOption? SelectedCurrencyOption { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? GroupImagePath { get; private set; }

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
            GroupImagePath = details.ImagePath;
            BuildCurrencyList(details.Currency);
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

        if (SelectedCurrencyOption is null)
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
            await _dataService.UpdateGroupAsync(_groupId, GroupName, SelectedCurrencyOption.Code);
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

    private async void OnChangePhotoClicked(object? sender, EventArgs e)
    {
        if (_groupId is null) return;

        var choice = await DisplayActionSheetAsync(
            AppResources.GroupDetails_PhotoSectionTitle,
            AppResources.Common_Cancel,
            null,
            AppResources.GroupDetails_PhotoFromCamera,
            AppResources.GroupDetails_PhotoFromGallery,
            string.IsNullOrEmpty(GroupImagePath) ? null : AppResources.GroupDetails_PhotoRemove);

        if (string.IsNullOrEmpty(choice) || choice == AppResources.Common_Cancel)
            return;

        try
        {
            if (choice == AppResources.GroupDetails_PhotoRemove)
            {
                await RemoveGroupPhotoAsync();
                return;
            }

            FileResult? result = choice == AppResources.GroupDetails_PhotoFromCamera
                ? await MediaPicker.Default.CapturePhotoAsync()
                : await MediaPicker.Default.PickPhotoAsync();

            if (result is null) return;

            var dir = Path.Combine(FileSystem.AppDataDirectory, "group_images");
            Directory.CreateDirectory(dir);
            var destPath = Path.Combine(dir, $"{_groupId}.jpg");

            await using (var src = await result.OpenReadAsync())
            await using (var dst = File.OpenWrite(destPath))
            {
                await src.CopyToAsync(dst);
            }

            await _dataService.SaveGroupImageAsync(_groupId, destPath);
            GroupImagePath = destPath;
            OnPropertyChanged(nameof(GroupImagePath));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async Task RemoveGroupPhotoAsync()
    {
        if (_groupId is null) return;

        if (!string.IsNullOrEmpty(GroupImagePath) && File.Exists(GroupImagePath))
            File.Delete(GroupImagePath);

        await _dataService.SaveGroupImageAsync(_groupId, null);
        GroupImagePath = null;
        OnPropertyChanged(nameof(GroupImagePath));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void BuildCurrencyList(string preferredCurrencyCode)
    {
        CurrencyOptions.Clear();
        foreach (var option in CurrencyCatalog.GetSupportedCurrencyOptions())
        {
            CurrencyOptions.Add(option);
        }

        var selected = CurrencyCatalog.FindByCode(CurrencyOptions, preferredCurrencyCode);
        if (selected is null)
        {
            selected = CurrencyCatalog.GetOrCreateOption(preferredCurrencyCode);
            CurrencyOptions.Add(selected);
        }

        SelectedCurrencyOption = selected;
        OnPropertyChanged(nameof(SelectedCurrencyOption));
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SelectedCurrencyOption));
        OnPropertyChanged(nameof(GroupImagePath));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanArchive));
        OnPropertyChanged(nameof(StatusText));
    }
}
