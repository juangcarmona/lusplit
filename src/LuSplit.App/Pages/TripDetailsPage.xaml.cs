using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace LuSplit.App.Pages;

public partial class TripDetailsPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private string _mode = EditMode;
    private string? _groupId;

    private const string CreateMode = "create";
    private const string EditMode = "edit";

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };

    public ObservableCollection<TripPersonEditorViewModel> People { get; } = new();

    public string TripName { get; set; } = string.Empty;

    public string? SelectedCurrency { get; set; } = "USD";

    public string NewPersonName { get; set; } = string.Empty;

    public string NewHouseholdName { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string PageTitle => IsCreateMode ? AppResources.TripDetails_PageTitleCreate : AppResources.TripDetails_PageTitleEdit;

    public bool CanExport => !IsCreateMode;

    public string PageSubtitle => IsCreateMode
        ? AppResources.TripDetails_SubtitleCreate
        : AppResources.TripDetails_SubtitleEdit;

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
        _mode = query.TryGetValue("mode", out var value) && string.Equals(value?.ToString(), CreateMode, StringComparison.OrdinalIgnoreCase)
            ? CreateMode
            : EditMode;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        StatusText = string.Empty;

        if (IsCreateMode)
        {
            _groupId = null;
            TripName = string.Empty;
            SelectedCurrency = SelectedCurrency is null ? "USD" : SelectedCurrency;
            People.Clear();
        }
        else
        {
            var details = await _dataService.GetTripDetailsAsync();
            _groupId = details.GroupId;
            TripName = details.TripName;
            EnsureCurrencyOption(details.Currency);
            SelectedCurrency = details.Currency;

            People.Clear();
            foreach (var person in details.Members)
            {
                People.Add(new TripPersonEditorViewModel(person.Name, person.HouseholdName, false));
            }
        }

        NewPersonName = string.Empty;
        NewHouseholdName = string.Empty;
        Title = PageTitle;

        OnPropertyChanged(nameof(TripName));
        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(NewPersonName));
        OnPropertyChanged(nameof(NewHouseholdName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(CanExport));
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

            if (IsCreateMode)
            {
                People.Add(new TripPersonEditorViewModel(
                    NewPersonName.Trim(),
                    string.IsNullOrWhiteSpace(NewHouseholdName) ? AppResources.TripDetails_SettlesOnOwn : NewHouseholdName.Trim(),
                    true));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = AppResources.Validation_TripNotFound;
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.AddTripMemberAsync(_groupId, NewPersonName, NewHouseholdName);
                StatusText = AppResources.TripDetails_PersonAdded;
                await LoadAsync();
            }

            NewPersonName = string.Empty;
            NewHouseholdName = string.Empty;
            StatusText = IsCreateMode ? AppResources.TripDetails_PersonAddedNew : StatusText;
            OnPropertyChanged(nameof(NewPersonName));
            OnPropertyChanged(nameof(NewHouseholdName));
            OnPropertyChanged(nameof(StatusText));
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
                            : person.HouseholdText)).ToArray());

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

    public sealed record TripPersonEditorViewModel(string Name, string HouseholdText, bool CanRemove);
}