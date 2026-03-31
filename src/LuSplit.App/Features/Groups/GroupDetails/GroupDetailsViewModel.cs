using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Formatting;
using LuSplit.App.Services.Persistence;
using LuSplit.App.Services.Settings;

namespace LuSplit.App.Pages;

public sealed partial class GroupDetailsViewModel : ObservableObject
{
    private readonly IGroupDetailsDataService _dataService;
    private string? _groupId;
    private string? _overrideGroupId;

    // ── Observable state ──────────────────────────────────────────────────

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public ObservableCollection<ParticipantDraftViewModel> Participants { get; } = new();

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanArchive))]
    private bool _isArchived;

    [ObservableProperty]
    private CurrencyOption? _selectedCurrencyOption;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _groupImagePath;

    // ── Derived ────────────────────────────────────────────────────────────

    public bool CanEdit => !IsArchived;
    public bool CanArchive => !IsArchived;

    // ── Events raised for UI-only concerns (dialogs, navigation, media) ──

    /// <summary>Fires when the group was saved. Code-behind navigates back.</summary>
    public event EventHandler? SaveCompleted;

    /// <summary>Fires when the group was archived. Code-behind navigates to home.</summary>
    public event EventHandler? ArchiveCompleted;

    /// <summary>
    /// Fires when the VM needs an archive confirmation.
    /// Code-behind shows the alert and calls <see cref="ConfirmArchiveAsync"/>.
    /// </summary>
    public event EventHandler? ArchiveConfirmationRequested;

    /// <summary>
    /// Fires when the VM needs the export flow to run.
    /// Code-behind calls GroupExportService with the active group id.
    /// </summary>
    public event EventHandler<string>? ExportRequested;

    /// <summary>
    /// Fires when the VM needs the photo-source action sheet.
    /// Code-behind shows the sheet and calls <see cref="ApplyPhotoPickAsync"/> or <see cref="RemovePhotoAsync"/>.
    /// </summary>
    public event EventHandler? PhotoChangeRequested;

    // ── Dependencies ──────────────────────────────────────────────────────

    public string? GroupId => _groupId;

    public GroupDetailsViewModel(IGroupDetailsDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>Stores an optional override group id before <see cref="LoadAsync"/> is called.</summary>
    public void SetOverrideGroupId(string? groupId) => _overrideGroupId = groupId;

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var details = _overrideGroupId is not null
                ? await _dataService.GetGroupDetailsAsync(_overrideGroupId)
                : await _dataService.GetGroupDetailsAsync();

            _groupId = details.GroupId;
            IsArchived = details.IsArchived;
            GroupName = details.GroupName;
            GroupImagePath = details.ImagePath;
            BuildCurrencyOptions(details.Currency);

            var preferredName = UserProfilePreferences.GetPreferredName();
            BuildParticipants(details.Members, preferredName);

            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            StatusText = AppResources.Validation_GroupNameRequired;
            return;
        }

        if (SelectedCurrencyOption is null)
        {
            StatusText = AppResources.Validation_SelectCurrency;
            return;
        }

        if (string.IsNullOrWhiteSpace(_groupId))
        {
            StatusText = AppResources.Validation_GroupNotFound;
            return;
        }

        try
        {
            await _dataService.UpdateGroupAsync(_groupId, GroupName, SelectedCurrencyOption.Code);
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void RequestArchive()
    {
        if (string.IsNullOrWhiteSpace(_groupId)) return;
        ArchiveConfirmationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by code-behind after the user confirms the archive alert.</summary>
    public async Task ConfirmArchiveAsync()
    {
        if (string.IsNullOrWhiteSpace(_groupId)) return;
        try
        {
            await _dataService.ArchiveGroupAsync(_groupId);
            ArchiveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void RequestExport()
    {
        if (_groupId is null) return;
        ExportRequested?.Invoke(this, _groupId);
    }

    [RelayCommand]
    private void RequestPhotoChange()
    {
        if (_groupId is null) return;
        PhotoChangeRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by code-behind after a successful photo pick/capture.</summary>
    public void ApplyNewPhoto(string destPath)
    {
        GroupImagePath = destPath;
    }

    /// <summary>Called by code-behind after photo removal.</summary>
    public void ApplyPhotoRemoved()
    {
        GroupImagePath = null;
    }

    // ── Participant operations ─────────────────────────────────────────────

    public async Task AddMemberAsync(string name)
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
        }
    }

    public async Task UpdateMemberDependencyAsync(ParticipantDraftViewModel participant)
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
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            // Revert to server state on error; set status after reload so it isn't cleared
            await LoadAsync();
            StatusText = errorMessage;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void BuildCurrencyOptions(string currentCurrencyCode)
    {
        CurrencyCatalog.PopulateSupportedOptions(CurrencyOptions);

        var selected = CurrencyCatalog.FindByCode(CurrencyOptions, currentCurrencyCode);
        if (selected is null)
        {
            selected = CurrencyCatalog.GetOrCreateOption(currentCurrencyCode);
            CurrencyOptions.Add(selected);
        }

        SelectedCurrencyOption = selected;
    }

    internal static IEnumerable<ParticipantDraftViewModel> BuildSortedParticipants(
        IReadOnlyList<GroupMemberModel> members,
        string? preferredName)
    {
        foreach (var entry in GroupDetailsParticipantSorter.Sort(members, preferredName))
            yield return new ParticipantDraftViewModel(entry.Name, entry.ParticipantId, canRemove: false)
            {
                DependsOn = entry.DependsOn
            };
    }

    private void BuildParticipants(IReadOnlyList<GroupMemberModel> members, string? preferredName)
    {
        Participants.Clear();
        foreach (var vm in BuildSortedParticipants(members, preferredName))
            Participants.Add(vm);
    }
}
