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
    private string? _selectedPersonParticipantId;

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };

    public ObservableCollection<GroupPersonEditorViewModel> People { get; } = new();

    // Consumption category options shown in the Add Person picker.
    // Using an enum-backed view model so the picker comparison is stable
    // regardless of the active locale — avoids comparing localized strings.
    public ObservableCollection<ConsumptionOptionViewModel> ConsumptionOptions { get; }

    public string GroupName { get; set; } = string.Empty;

    public string? SelectedCurrency { get; set; } = "USD";

    public string NewPersonName { get; set; } = string.Empty;

    public string? SelectedResponsibleParticipantName { get; set; }

    public ObservableCollection<string> ResponsibleParticipantOptions { get; } = new();

    public ConsumptionOptionViewModel SelectedConsumption { get; set; }

    public string NewCustomWeight { get; set; } = string.Empty;

    public string EditPersonName { get; set; } = string.Empty;

    public string? EditSelectedResponsibleParticipantName { get; set; }

    public ObservableCollection<string> EditResponsibleParticipantOptions { get; } = new();

    public bool CanChangeSelectedPersonDependency { get; set; } = true;

    public string EditPersonDependencyStatusText { get; set; } = string.Empty;

    public bool IsEditPersonDependencyStatusVisible => !string.IsNullOrWhiteSpace(EditPersonDependencyStatusText);

    public string StatusText { get; set; } = string.Empty;

    public bool IsArchived => _isArchived;

    /// <summary>True when the group can be edited — create mode is always editable; edit mode requires the group to not be archived.</summary>
    public bool CanEdit => IsCreateMode || !_isArchived;
    
    public bool CanManagePeople => CanEdit && !IsCreateMode;

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
            foreach (var person in GroupDetailsPeopleService.BuildPeopleViewModels(details.Members))
            {
                People.Add(person);
            }
        }

        NewPersonName = string.Empty;
        SelectedResponsibleParticipantName = null;
        SelectedConsumption = ConsumptionOptions[0];
        NewCustomWeight = string.Empty;
        ResetPersonEditor();
        Title = PageTitle;
        RebuildResponsibleParticipantOptions();

        NotifyAllProperties();
    }

    private async void OnAddPersonClicked(object? sender, EventArgs e)
    {
        try
        {
            var personName = await DisplayPromptAsync(
                AppResources.GroupDetails_AddPersonSection,
                AppResources.GroupDetails_PersonNamePrompt,
                AppResources.GroupDetails_AddPersonButton,
                AppResources.Common_Cancel,
                AppResources.GroupDetails_PersonNamePlaceholder);

            if (personName is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(personName))
            {
                StatusText = AppResources.Validation_PersonNameRequired;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var newPersonName = personName.Trim();
            if (People.Any(person => string.Equals(person.Name, newPersonName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText = AppResources.Validation_PersonNameMustBeUnique;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var dependsOnSelection = await DisplayActionSheetAsync(
                AppResources.GroupDetails_DependsOnLabel,
                AppResources.Common_Cancel,
                AppResources.GroupDetails_DependsOnPlaceholder,
                ResponsibleParticipantOptions.ToArray());
            if (string.Equals(dependsOnSelection, AppResources.Common_Cancel, StringComparison.Ordinal))
            {
                return;
            }

            SelectedResponsibleParticipantName = string.Equals(dependsOnSelection, AppResources.GroupDetails_DependsOnPlaceholder, StringComparison.Ordinal)
                ? null
                : dependsOnSelection;

            var consumptionSelection = await DisplayActionSheetAsync(
                AppResources.GroupDetails_ConsumptionCategoryLabel,
                AppResources.Common_Cancel,
                null,
                AppResources.GroupDetails_ConsumptionFull,
                AppResources.GroupDetails_ConsumptionHalf,
                AppResources.GroupDetails_ConsumptionCustom);
            if (string.Equals(consumptionSelection, AppResources.Common_Cancel, StringComparison.Ordinal))
            {
                return;
            }

            var category = consumptionSelection switch
            {
                var value when string.Equals(value, AppResources.GroupDetails_ConsumptionHalf, StringComparison.Ordinal)
                    => ConsumptionCategory.Half,
                var value when string.Equals(value, AppResources.GroupDetails_ConsumptionCustom, StringComparison.Ordinal)
                    => ConsumptionCategory.Custom,
                _ => ConsumptionCategory.Full
            };

            string? customWeight = null;
            if (category == ConsumptionCategory.Custom)
            {
                var customWeightSelection = await DisplayPromptAsync(
                    AppResources.GroupDetails_ConsumptionCustom,
                    AppResources.Validation_InvalidCustomWeight,
                    AppResources.GroupDetails_AddPersonButton,
                    AppResources.Common_Cancel,
                    AppResources.GroupDetails_CustomWeightPlaceholder,
                    keyboard: Keyboard.Numeric);
                if (customWeightSelection is null)
                {
                    return;
                }

                customWeight = customWeightSelection.Trim();
                if (string.IsNullOrWhiteSpace(customWeight)
                    || !decimal.TryParse(customWeight, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    StatusText = AppResources.Validation_InvalidCustomWeight;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }
            }

            if (IsCreateMode)
            {
                // Seed value; Create mode immediately recalculates relationship text for all draft people.
                People.Add(new GroupPersonEditorViewModel(
                    null,
                    newPersonName,
                    ResolveSelectedHouseholdName(),
                    true,
                    AppResources.GroupDetails_ResponsibilityIndependent,
                    false,
                    true,
                    CategoryToString(category),
                    customWeight));
                RebuildDraftPeopleRelationships();
                RebuildResponsibleParticipantOptions();
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
                    newPersonName,
                    ResolveSelectedHouseholdName(),
                    category,
                    customWeight);
                StatusText = AppResources.GroupDetails_PersonAdded;
                await LoadAsync();
            }

            NewPersonName = string.Empty;
            SelectedResponsibleParticipantName = null;
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
            RebuildResponsibleParticipantOptions();
        }
    }

    private async void OnManagePersonClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!CanEdit || IsCreateMode || sender is not Button { CommandParameter: string participantId })
            {
                return;
            }

            var person = People.FirstOrDefault(candidate => string.Equals(candidate.ParticipantId, participantId, StringComparison.Ordinal));
            if (person is null)
            {
                return;
            }

            OpenPersonEditor(person);
            await ShowManagePersonDialogAsync(person);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async Task SavePersonChangesAsync()
    {
        try
        {
            var selectedPerson = GetSelectedPersonForEdit();
            if (selectedPerson is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(EditPersonName))
            {
                StatusText = AppResources.Validation_PersonNameRequired;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var normalizedName = EditPersonName.Trim();
            if (People.Any(person =>
                    !string.Equals(person.ParticipantId, selectedPerson.ParticipantId, StringComparison.Ordinal)
                    && string.Equals(person.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText = AppResources.Validation_PersonNameMustBeUnique;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (string.IsNullOrWhiteSpace(_groupId) || string.IsNullOrWhiteSpace(selectedPerson.ParticipantId))
            {
                StatusText = AppResources.Validation_GroupNotFound;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!string.IsNullOrWhiteSpace(EditSelectedResponsibleParticipantName)
                && IsCircularSelection(selectedPerson, EditSelectedResponsibleParticipantName))
            {
                StatusText = AppResources.Validation_CircularDependency;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var dependsOnParticipantId = ResolveEditResponsibleParticipantId(selectedPerson);
            await _dataService.UpdateGroupMemberAsync(_groupId, selectedPerson.ParticipantId, normalizedName, dependsOnParticipantId);
            StatusText = AppResources.GroupDetails_PersonUpdated;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async Task ShowManagePersonDialogAsync(GroupPersonEditorViewModel person)
    {
        var editedName = await DisplayPromptAsync(
            AppResources.GroupDetails_ManagePersonSection,
            AppResources.GroupDetails_PersonNamePrompt,
            AppResources.GroupDetails_SavePersonChangesButton,
            AppResources.Common_Cancel,
            AppResources.GroupDetails_PersonNamePlaceholder,
            initialValue: person.Name);
        if (editedName is null)
        {
            ResetPersonEditor();
            NotifyPersonEditorProperties();
            return;
        }

        EditPersonName = editedName.Trim();

        var dependencyOptions = new[] { AppResources.GroupDetails_DependencyIndependent }.Concat(EditResponsibleParticipantOptions).ToArray();
        var dependsOnSelection = await DisplayActionSheetAsync(
            AppResources.GroupDetails_DependsOnLabel,
            AppResources.Common_Cancel,
            null,
            dependencyOptions);

        if (string.Equals(dependsOnSelection, AppResources.Common_Cancel, StringComparison.Ordinal))
        {
            ResetPersonEditor();
            NotifyPersonEditorProperties();
            return;
        }

        EditSelectedResponsibleParticipantName = string.Equals(dependsOnSelection, AppResources.GroupDetails_DependencyIndependent, StringComparison.Ordinal)
            ? null
            : dependsOnSelection;

        await SavePersonChangesAsync();
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

                await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
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
            await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
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

    private string? ResolveSelectedHouseholdName()
    {
        if (string.IsNullOrWhiteSpace(SelectedResponsibleParticipantName))
        {
            return null;
        }

        if (!GroupDetailsDependencyService.IsEligibleResponsibleParticipantName(SelectedResponsibleParticipantName, ResponsibleParticipantOptions))
        {
            return null;
        }

        var responsible = People.FirstOrDefault(person =>
            string.Equals(person.Name, SelectedResponsibleParticipantName, StringComparison.OrdinalIgnoreCase));
        return responsible?.HouseholdName ?? responsible?.Name;
    }

    private GroupPersonEditorViewModel? GetSelectedPersonForEdit()
    {
        return People.FirstOrDefault(person =>
            string.Equals(person.ParticipantId, _selectedPersonParticipantId, StringComparison.Ordinal));
    }

    private void RebuildDraftPeopleRelationships()
    {
        if (!IsCreateMode || People.Count == 0)
        {
            return;
        }

        var rebuilt = GroupDetailsDependencyService.RebuildDraftPeopleRelationships(People.ToArray());

        People.Clear();
        foreach (var person in rebuilt)
        {
            People.Add(person);
        }
    }

    private void RebuildResponsibleParticipantOptions()
    {
        var previousSelection = SelectedResponsibleParticipantName;
        ResponsibleParticipantOptions.Clear();
        foreach (var candidate in GroupDetailsDependencyService.BuildResponsibleParticipantOptions(People.ToArray()))
        {
            ResponsibleParticipantOptions.Add(candidate);
        }

        SelectedResponsibleParticipantName = previousSelection is not null
            && GroupDetailsDependencyService.IsEligibleResponsibleParticipantName(previousSelection, ResponsibleParticipantOptions)
            ? previousSelection
            : null;

        OnPropertyChanged(nameof(ResponsibleParticipantOptions));
        OnPropertyChanged(nameof(SelectedResponsibleParticipantName));
    }

    private void OpenPersonEditor(GroupPersonEditorViewModel person)
    {
        _selectedPersonParticipantId = person.ParticipantId;
        EditPersonName = person.Name;
        RebuildEditResponsibleParticipantOptions(person);
        EditSelectedResponsibleParticipantName = ResolveCurrentResponsibleName(person);
        CanChangeSelectedPersonDependency = CanEditDependency(person);
        EditPersonDependencyStatusText = CanChangeSelectedPersonDependency
            ? string.Empty
            : AppResources.GroupDetails_EditDependencyLockedHint;
        NotifyPersonEditorProperties();
    }

    private bool CanEditDependency(GroupPersonEditorViewModel person)
        => GroupDetailsDependencyService.CanEditDependency(person, People.ToArray());

    private string? ResolveCurrentResponsibleName(GroupPersonEditorViewModel person)
    {
        return GroupDetailsDependencyService.ResolveCurrentResponsibleName(person, People.ToArray());
    }

    private void RebuildEditResponsibleParticipantOptions(GroupPersonEditorViewModel selectedPerson)
    {
        EditResponsibleParticipantOptions.Clear();
        foreach (var option in GroupDetailsDependencyService.BuildEditResponsibleParticipantOptions(selectedPerson, People.ToArray()))
        {
            EditResponsibleParticipantOptions.Add(option);
        }
    }

    private string? ResolveEditResponsibleParticipantId(GroupPersonEditorViewModel selectedPerson)
    {
        return GroupDetailsDependencyService.ResolveEditResponsibleParticipantId(
            selectedPerson,
            EditSelectedResponsibleParticipantName,
            CanChangeSelectedPersonDependency,
            People.ToArray());
    }

    private string? ResolveCurrentResponsibleParticipantId(GroupPersonEditorViewModel selectedPerson)
    {
        return GroupDetailsDependencyService.ResolveCurrentResponsibleParticipantId(selectedPerson, People.ToArray());
    }

    private bool IsCircularSelection(GroupPersonEditorViewModel selectedPerson, string selectedResponsibleName)
    {
        return GroupDetailsDependencyService.IsCircularSelection(selectedPerson, selectedResponsibleName, People.ToArray());
    }

    private void ResetPersonEditor()
    {
        _selectedPersonParticipantId = null;
        EditPersonName = string.Empty;
        EditSelectedResponsibleParticipantName = null;
        EditResponsibleParticipantOptions.Clear();
        CanChangeSelectedPersonDependency = true;
        EditPersonDependencyStatusText = string.Empty;
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanManagePeople));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanArchive));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(SaveButtonText));
        NotifyAddPersonProperties();
        NotifyPersonEditorProperties();
        OnPropertyChanged(nameof(StatusText));
    }

    private void NotifyAddPersonProperties()
    {
        OnPropertyChanged(nameof(NewPersonName));
        OnPropertyChanged(nameof(SelectedResponsibleParticipantName));
        OnPropertyChanged(nameof(ResponsibleParticipantOptions));
        OnPropertyChanged(nameof(SelectedConsumption));
        OnPropertyChanged(nameof(NewCustomWeight));
        OnPropertyChanged(nameof(IsCustomConsumption));
        OnPropertyChanged(nameof(StatusText));
    }

    private void NotifyPersonEditorProperties()
    {
        OnPropertyChanged(nameof(EditPersonName));
        OnPropertyChanged(nameof(EditSelectedResponsibleParticipantName));
        OnPropertyChanged(nameof(EditResponsibleParticipantOptions));
        OnPropertyChanged(nameof(CanChangeSelectedPersonDependency));
        OnPropertyChanged(nameof(EditPersonDependencyStatusText));
        OnPropertyChanged(nameof(IsEditPersonDependencyStatusVisible));
    }
}
