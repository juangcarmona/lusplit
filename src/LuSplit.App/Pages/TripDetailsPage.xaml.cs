using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Entities;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace LuSplit.App.Pages;

public partial class TripDetailsPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private string _mode = EditMode;
    private string? _groupId;
    // Set when navigating from an archived trip view, to load a specific group
    // without changing the user's currently selected active trip.
    private string? _overrideGroupId;
    private bool _isArchived;

    private const string CreateMode = "create";
    private const string EditMode = "edit";

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };

    public ObservableCollection<TripPersonEditorViewModel> People { get; } = new();

    // Consumption category options shown in the Add Person picker.
    public ObservableCollection<string> ConsumptionOptions { get; } = new()
    {
        AppResources.TripDetails_ConsumptionFull,
        AppResources.TripDetails_ConsumptionHalf,
        AppResources.TripDetails_ConsumptionCustom
    };

    public string TripName { get; set; } = string.Empty;

    public string? SelectedCurrency { get; set; } = "USD";

    public string NewPersonName { get; set; } = string.Empty;

    public string NewHouseholdName { get; set; } = string.Empty;

    public string SelectedConsumptionOption { get; set; } = AppResources.TripDetails_ConsumptionFull;

    public string NewCustomWeight { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public bool IsArchived => _isArchived;

    /// <summary>True when the trip can be edited — i.e. it is not archived.</summary>
    public bool CanEdit => !IsCreateMode && !_isArchived;

    public bool CanExport => !IsCreateMode;

    /// <summary>Archive button is visible only in non-archived edit mode.</summary>
    public bool CanArchive => !IsCreateMode && !_isArchived;

    /// <summary>Custom-weight entry is visible only when "Custom weight" is selected.</summary>
    public bool IsCustomConsumption =>
        string.Equals(SelectedConsumptionOption, AppResources.TripDetails_ConsumptionCustom, StringComparison.Ordinal);

    public string PageTitle => IsCreateMode ? AppResources.TripDetails_PageTitleCreate : AppResources.TripDetails_PageTitleEdit;

    public string PageSubtitle => IsCreateMode
        ? AppResources.TripDetails_SubtitleCreate
        : (_isArchived ? AppResources.TripDetails_ArchivedStatus : AppResources.TripDetails_SubtitleEdit);

    public string SaveButtonText => IsCreateMode ? AppResources.TripDetails_CreateButton : AppResources.TripDetails_SaveButton;

    private bool IsCreateMode => string.Equals(_mode, CreateMode, StringComparison.OrdinalIgnoreCase);

    public TripDetailsPage(AppDataService dataService)
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
            TripName = string.Empty;
            SelectedCurrency = SelectedCurrency is null ? "USD" : SelectedCurrency;
            People.Clear();
        }
        else
        {
            var details = _overrideGroupId is not null
                ? await _dataService.GetTripDetailsAsync(_overrideGroupId)
                : await _dataService.GetTripDetailsAsync();

            _groupId = details.GroupId;
            _isArchived = details.IsArchived;
            TripName = details.TripName;
            EnsureCurrencyOption(details.Currency);
            SelectedCurrency = details.Currency;

            People.Clear();
            foreach (var person in details.Members)
            {
                People.Add(new TripPersonEditorViewModel(
                    person.Name,
                    person.HouseholdName,
                    false,
                    person.IsOwner,
                    person.ConsumptionCategory,
                    person.CustomConsumptionWeight));
            }
        }

        NewPersonName = string.Empty;
        NewHouseholdName = string.Empty;
        SelectedConsumptionOption = AppResources.TripDetails_ConsumptionFull;
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
                People.Add(new TripPersonEditorViewModel(
                    NewPersonName.Trim(),
                    string.IsNullOrWhiteSpace(NewHouseholdName) ? AppResources.TripDetails_SettlesOnOwn : NewHouseholdName.Trim(),
                    true,
                    false,
                    CategoryToString(category),
                    category == ConsumptionCategory.Custom ? NewCustomWeight.Trim() : null));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = AppResources.Validation_TripNotFound;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.AddTripMemberAsync(
                    _groupId,
                    NewPersonName,
                    NewHouseholdName,
                    category,
                    category == ConsumptionCategory.Custom ? NewCustomWeight.Trim() : null);
                StatusText = AppResources.TripDetails_PersonAdded;
                await LoadAsync();
            }

            NewPersonName = string.Empty;
            NewHouseholdName = string.Empty;
            SelectedConsumptionOption = AppResources.TripDetails_ConsumptionFull;
            NewCustomWeight = string.Empty;
            StatusText = IsCreateMode ? AppResources.TripDetails_PersonAddedNew : StatusText;
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
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TripName))
            {
                StatusText = AppResources.Validation_TripNameRequired;
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

                await _dataService.CreateTripAsync(
                    TripName,
                    SelectedCurrency,
                    People.Select(person => new TripDraftMember(
                        person.Name,
                        string.Equals(person.HouseholdText, AppResources.TripDetails_SettlesOnOwn, StringComparison.Ordinal)
                            ? null
                            : person.HouseholdText,
                        StringToCategory(person.ConsumptionCategory),
                        person.CustomConsumptionWeight)).ToArray());

                await Shell.Current.GoToAsync("//trips");
                await Shell.Current.GoToAsync(AppRoutes.TripTimeline);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = AppResources.Validation_TripNotFound;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.UpdateTripAsync(_groupId, TripName, SelectedCurrency);
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

        var confirmed = await DisplayAlert(
            AppResources.TripDetails_ArchiveConfirmTitle,
            AppResources.TripDetails_ArchiveConfirmMessage,
            AppResources.TripDetails_ArchiveConfirmYes,
            AppResources.Common_Cancel);

        if (!confirmed) return;

        try
        {
            await _dataService.ArchiveTripAsync(_groupId);
            await Shell.Current.GoToAsync("//trips");
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

        var choice = await DisplayActionSheet(
            AppResources.Export_DialogTitle,
            AppResources.Common_Cancel,
            null,
            AppResources.Export_JsonOption,
            AppResources.Export_CsvOption,
            AppResources.Export_PdfOption);

        if (string.IsNullOrEmpty(choice) || choice == AppResources.Common_Cancel) return;

        ExportFormat? format = null;
        if (choice == AppResources.Export_JsonOption) format = ExportFormat.Json;
        else if (choice == AppResources.Export_CsvOption) format = ExportFormat.Csv;
        else if (choice == AppResources.Export_PdfOption) format = ExportFormat.Pdf;

        if (format is null) return;

        try
        {
            var result = await _dataService.ExportTripAsync(_groupId, format.Value);
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
    {
        if (string.Equals(SelectedConsumptionOption, AppResources.TripDetails_ConsumptionHalf, StringComparison.Ordinal))
            return ConsumptionCategory.Half;
        if (string.Equals(SelectedConsumptionOption, AppResources.TripDetails_ConsumptionCustom, StringComparison.Ordinal))
            return ConsumptionCategory.Custom;
        return ConsumptionCategory.Full;
    }

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

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(TripName));
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
        OnPropertyChanged(nameof(SelectedConsumptionOption));
        OnPropertyChanged(nameof(NewCustomWeight));
        OnPropertyChanged(nameof(IsCustomConsumption));
        OnPropertyChanged(nameof(StatusText));
    }
}

public sealed record TripPersonEditorViewModel(
    string Name,
    string HouseholdText,
    bool CanRemove,
    bool IsOwner = false,
    string ConsumptionCategory = "FULL",
    string? CustomConsumptionWeight = null)
{
    /// <summary>Shows "Payer" for the economic unit owner, "Dependent" for other members of a shared household.
    /// Only shown in edit mode (items loaded from the database, not items staged in create mode).</summary>
    public bool ShowRoleBadge => !CanRemove;

    public string RoleBadge => IsOwner
        ? AppResources.TripDetails_PersonIsOwner
        : AppResources.TripDetails_PersonIsDependent;

    public string ConsumptionLabel => ConsumptionCategory switch
    {
        "HALF" => AppResources.TripDetails_ConsumptionHalf,
        "CUSTOM" => $"{AppResources.TripDetails_ConsumptionCustom}: {CustomConsumptionWeight}",
        _ => AppResources.TripDetails_ConsumptionFull
    };
}