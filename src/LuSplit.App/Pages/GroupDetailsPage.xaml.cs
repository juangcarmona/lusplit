using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Entities;

namespace LuSplit.App.Pages;

public partial class GroupDetailsPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private string _mode = EditMode;
    private string? _groupId;
    // Set when navigating from an archived group view, to load a specific group
    // without changing the user's currently selected active group.
    private string? _overrideGroupId;
    private bool _isArchived;

    private const string CreateMode = "create";
    private const string EditMode = "edit";

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };

    public ObservableCollection<GroupPersonEditorViewModel> People { get; } = new();

    // Consumption category options shown in the Add Person picker.
    // Using an enum-backed view model so the picker comparison is stable
    // regardless of the active locale — avoids comparing localized strings.
    public ObservableCollection<ConsumptionOptionViewModel> ConsumptionOptions { get; }

    public string GroupName { get; set; } = string.Empty;

    public string? SelectedCurrency { get; set; } = "USD";

    public string NewPersonName { get; set; } = string.Empty;

    public string NewHouseholdName { get; set; } = string.Empty;

    public ConsumptionOptionViewModel SelectedConsumption { get; set; }

    public string NewCustomWeight { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public bool IsArchived => _isArchived;

    /// <summary>True when the group can be edited — create mode is always editable; edit mode requires the group to not be archived.</summary>
    public bool CanEdit => IsCreateMode || !_isArchived;

    public bool CanExport => !IsCreateMode;

    /// <summary>Archive button is visible only in non-archived edit mode.</summary>
    public bool CanArchive => !IsCreateMode && !_isArchived;

    /// <summary>Custom-weight entry is visible only when "Custom weight" is selected.</summary>
    public bool IsCustomConsumption => SelectedConsumption?.Category == ConsumptionCategory.Custom;

    public string PageTitle => IsCreateMode ? AppResources.GroupDetails_PageTitleCreate : AppResources.GroupDetails_PageTitleEdit;

    public string PageSubtitle => IsCreateMode
        ? AppResources.GroupDetails_SubtitleCreate
        : (_isArchived ? AppResources.GroupDetails_ArchivedStatus : AppResources.GroupDetails_SubtitleEdit);

    public string SaveButtonText => IsCreateMode ? AppResources.GroupDetails_CreateButton : AppResources.GroupDetails_SaveButton;

    private bool IsCreateMode => string.Equals(_mode, CreateMode, StringComparison.OrdinalIgnoreCase);

    public GroupDetailsPage(AppDataService dataService)
    {
        _dataService = dataService;
        ConsumptionOptions = new()
        {
            new(ConsumptionCategory.Full, AppResources.GroupDetails_ConsumptionFull),
            new(ConsumptionCategory.Half, AppResources.GroupDetails_ConsumptionHalf),
            new(ConsumptionCategory.Custom, AppResources.GroupDetails_ConsumptionCustom),
        };
        SelectedConsumption = ConsumptionOptions[0];
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _mode = query.TryGetValue("mode", out var modeVal) && string.Equals(modeVal?.ToString(), CreateMode, StringComparison.OrdinalIgnoreCase)
            ? CreateMode
            : EditMode;

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
        StatusText = string.Empty;
        _isArchived = false;

        if (IsCreateMode)
        {
            _groupId = null;
            GroupName = string.Empty;
            SelectedCurrency = SelectedCurrency is null ? "USD" : SelectedCurrency;
            People.Clear();
        }
        else
        {
            var details = _overrideGroupId is not null
                ? await _dataService.GetGroupDetailsAsync(_overrideGroupId)
                : await _dataService.GetGroupDetailsAsync();

            _groupId = details.GroupId;
            _isArchived = details.IsArchived;
            GroupName = details.GroupName;
            EnsureCurrencyOption(details.Currency);
            SelectedCurrency = details.Currency;

            People.Clear();
            foreach (var person in BuildPeopleViewModels(details.Members))
            {
                People.Add(person);
            }
        }

        NewPersonName = string.Empty;
        NewHouseholdName = string.Empty;
        SelectedConsumption = ConsumptionOptions[0];
        NewCustomWeight = string.Empty;
        Title = PageTitle;

        NotifyAllProperties();
    }

    private async void OnAddPersonClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewPersonName))
            {
                StatusText = AppResources.Validation_PersonNameRequired;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var category = ResolveConsumptionCategory();

            if (category == ConsumptionCategory.Custom)
            {
                if (string.IsNullOrWhiteSpace(NewCustomWeight) || !decimal.TryParse(NewCustomWeight, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    StatusText = AppResources.Validation_InvalidCustomWeight;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }
            }

            if (IsCreateMode)
            {
                People.Add(new GroupPersonEditorViewModel(
                    NewPersonName.Trim(),
                    string.IsNullOrWhiteSpace(NewHouseholdName) ? null : NewHouseholdName.Trim(),
                    true,
                    AppResources.GroupDetails_ResponsibilityIndependent,
                    CategoryToString(category),
                    category == ConsumptionCategory.Custom ? NewCustomWeight.Trim() : null));
                RebuildDraftPeopleRelationships();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = AppResources.Validation_GroupNotFound;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.AddGroupMemberAsync(
                    _groupId,
                    NewPersonName,
                    NewHouseholdName,
                    category,
                    category == ConsumptionCategory.Custom ? NewCustomWeight.Trim() : null);
                StatusText = AppResources.GroupDetails_PersonAdded;
                await LoadAsync();
            }

            NewPersonName = string.Empty;
            NewHouseholdName = string.Empty;
            SelectedConsumption = ConsumptionOptions[0];
            NewCustomWeight = string.Empty;
            StatusText = IsCreateMode ? AppResources.GroupDetails_PersonAddedNew : StatusText;
            NotifyAddPersonProperties();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnRemovePersonClicked(object? sender, EventArgs e)
    {
        if (!IsCreateMode || sender is not Button { CommandParameter: string personName })
        {
            return;
        }

        var item = People.FirstOrDefault(person => string.Equals(person.Name, personName, StringComparison.Ordinal));
        if (item is not null)
        {
            People.Remove(item);
            RebuildDraftPeopleRelationships();
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
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

            if (IsCreateMode)
            {
                if (People.Count == 0)
                {
                    StatusText = AppResources.Validation_AddAtLeastOnePerson;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.CreateGroupAsync(
                    GroupName,
                    SelectedCurrency,
                    People.Select(person => new GroupDraftMember(
                        person.Name,
                        string.IsNullOrEmpty(person.HouseholdName) ? null : person.HouseholdName,
                        StringToCategory(person.ConsumptionCategory),
                        person.CustomConsumptionWeight)).ToArray());

                await Shell.Current.GoToAsync("//groups");
                await Shell.Current.GoToAsync(AppRoutes.GroupTimeline);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = AppResources.Validation_GroupNotFound;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.UpdateGroupAsync(_groupId, GroupName, SelectedCurrency);
                await Shell.Current.GoToAsync("..");
            }
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
            await Shell.Current.GoToAsync("//groups");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void EnsureCurrencyOption(string currency)
    {
        if (!CurrencyOptions.Contains(currency, StringComparer.OrdinalIgnoreCase))
        {
            CurrencyOptions.Add(currency.ToUpperInvariant());
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_groupId is null) return;

        var exportOptions = new (string Label, ExportFormat Format)[]
        {
            (AppResources.Export_JsonOption, ExportFormat.Json),
            (AppResources.Export_CsvOption, ExportFormat.Csv),
            (AppResources.Export_PdfOption, ExportFormat.Pdf),
        };

        var choice = await DisplayActionSheetAsync(
            AppResources.Export_DialogTitle,
            AppResources.Common_Cancel,
            null,
            exportOptions.Select(o => o.Label).ToArray());

        if (string.IsNullOrEmpty(choice) || choice == AppResources.Common_Cancel) return;

        var selected = Array.Find(exportOptions, o => string.Equals(o.Label, choice, StringComparison.Ordinal));
        if (selected.Label is null) return;

        try
        {
            var result = await _dataService.ExportGroupAsync(_groupId, selected.Format);
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = AppResources.Export_ShareTitle,
                File = new ShareFile(result.FilePath, result.MimeType)
            });
        }
        catch (Exception ex)
        {
            StatusText = string.Format(AppResources.Export_Failed, ex.Message);
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private ConsumptionCategory ResolveConsumptionCategory()
        => SelectedConsumption?.Category ?? ConsumptionCategory.Full;

    private static string CategoryToString(ConsumptionCategory category)
        => category switch
        {
            ConsumptionCategory.Half => "HALF",
            ConsumptionCategory.Custom => "CUSTOM",
            _ => "FULL"
        };

    private static ConsumptionCategory StringToCategory(string value)
        => value switch
        {
            "HALF" => ConsumptionCategory.Half,
            "CUSTOM" => ConsumptionCategory.Custom,
            _ => ConsumptionCategory.Full
        };

    private static IReadOnlyList<GroupPersonEditorViewModel> BuildPeopleViewModels(IReadOnlyList<GroupMemberModel> members)
    {
        var memberCountsByResponsibility = members
            .GroupBy(member => member.HouseholdName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var ownerNameByResponsibility = members
            .Where(member => member.IsOwner)
            .GroupBy(member => member.HouseholdName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        return members
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(member => new GroupPersonEditorViewModel(
                member.Name,
                member.HouseholdName,
                false,
                ResolveRelationshipText(member, memberCountsByResponsibility, ownerNameByResponsibility),
                member.ConsumptionCategory,
                member.CustomConsumptionWeight))
            .ToArray();
    }

    private static string ResolveRelationshipText(
        GroupMemberModel member,
        IReadOnlyDictionary<string, int> memberCountsByResponsibility,
        IReadOnlyDictionary<string, string> ownerNameByResponsibility)
    {
        if (!memberCountsByResponsibility.TryGetValue(member.HouseholdName, out var peopleInResponsibility))
        {
            return AppResources.GroupDetails_ResponsibilityIndependent;
        }

        if (member.IsOwner)
        {
            var dependents = Math.Max(0, peopleInResponsibility - 1);
            return dependents == 0
                ? AppResources.GroupDetails_ResponsibilityIndependent
                : string.Format(AppResources.GroupDetails_ResponsibilityResponsibleForPeople, dependents);
        }

        var ownerName = ownerNameByResponsibility.TryGetValue(member.HouseholdName, out var owner)
            ? owner
            : member.HouseholdName;
        return string.Format(AppResources.GroupDetails_ResponsibilityDependsOn, ownerName);
    }

    private void RebuildDraftPeopleRelationships()
    {
        if (!IsCreateMode || People.Count == 0)
        {
            return;
        }

        var rebuilt = People
            .Select(person =>
            {
                if (string.IsNullOrWhiteSpace(person.HouseholdName))
                {
                    return person with { RelationshipText = AppResources.GroupDetails_ResponsibilityIndependent };
                }

                var owner = People.FirstOrDefault(candidate =>
                    string.Equals(candidate.HouseholdName, person.HouseholdName, StringComparison.OrdinalIgnoreCase));
                if (owner is null || string.Equals(owner.Name, person.Name, StringComparison.Ordinal))
                {
                    var dependents = People.Count(candidate =>
                        string.Equals(candidate.HouseholdName, person.HouseholdName, StringComparison.OrdinalIgnoreCase)) - 1;
                    return person with
                    {
                        RelationshipText = dependents <= 0
                            ? AppResources.GroupDetails_ResponsibilityIndependent
                            : string.Format(AppResources.GroupDetails_ResponsibilityResponsibleForPeople, dependents)
                    };
                }

                return person with { RelationshipText = string.Format(AppResources.GroupDetails_ResponsibilityDependsOn, owner.Name) };
            })
            .ToArray();

        People.Clear();
        foreach (var person in rebuilt)
        {
            People.Add(person);
        }
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanArchive));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(SaveButtonText));
        NotifyAddPersonProperties();
        OnPropertyChanged(nameof(StatusText));
    }

    private void NotifyAddPersonProperties()
    {
        OnPropertyChanged(nameof(NewPersonName));
        OnPropertyChanged(nameof(NewHouseholdName));
        OnPropertyChanged(nameof(SelectedConsumption));
        OnPropertyChanged(nameof(NewCustomWeight));
        OnPropertyChanged(nameof(IsCustomConsumption));
        OnPropertyChanged(nameof(StatusText));
    }
}

public sealed record GroupPersonEditorViewModel(
    string Name,
    string? HouseholdName,  // null or empty means "settles on own" — avoids localized-string comparison
    bool CanRemove,
    string RelationshipText,
    string ConsumptionCategory = "FULL",
    string? CustomConsumptionWeight = null)
{
    public string ConsumptionLabel => ConsumptionCategory switch
    {
        "HALF" => AppResources.GroupDetails_ConsumptionHalf,
        "CUSTOM" => $"{AppResources.GroupDetails_ConsumptionCustom}: {CustomConsumptionWeight}",
        _ => AppResources.GroupDetails_ConsumptionFull
    };
}

/// <summary>
/// Stable enum-backed option for the consumption category picker.
/// Using <see cref="Category"/> for comparisons avoids binding to localized labels.
/// </summary>
public sealed record ConsumptionOptionViewModel(ConsumptionCategory Category, string Label);
