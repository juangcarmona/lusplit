using System.Collections.ObjectModel;
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

    public string PageTitle => IsCreateMode ? "New trip" : "Trip details";

    public bool CanExport => !IsCreateMode;

    public string PageSubtitle => IsCreateMode
        ? "Set up the trip once, then start adding events."
        : "Update the basics and keep the trip ready for the next event.";

    public string SaveButtonText => IsCreateMode ? "Create trip" : "Save trip";

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
                StatusText = "Person name is required.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (IsCreateMode)
            {
                People.Add(new TripPersonEditorViewModel(
                    NewPersonName.Trim(),
                    string.IsNullOrWhiteSpace(NewHouseholdName) ? "Settles on their own" : NewHouseholdName.Trim(),
                    true));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = "Trip not found.";
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.AddTripMemberAsync(_groupId, NewPersonName, NewHouseholdName);
                StatusText = "Person added.";
                await LoadAsync();
            }

            NewPersonName = string.Empty;
            NewHouseholdName = string.Empty;
            StatusText = IsCreateMode ? "Person added to the new trip." : StatusText;
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
                StatusText = "Trip name is required.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedCurrency))
            {
                StatusText = "Choose a currency.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (IsCreateMode)
            {
                if (People.Count == 0)
                {
                    StatusText = "Add at least one person.";
                    OnPropertyChanged(nameof(StatusText));
                    return;
                }

                await _dataService.CreateTripAsync(
                    TripName,
                    SelectedCurrency,
                    People.Select(person => new TripDraftMember(
                        person.Name,
                        string.Equals(person.HouseholdText, "Settles on their own", StringComparison.Ordinal)
                            ? null
                            : person.HouseholdText)).ToArray());

                await Shell.Current.GoToAsync("//trips");
                await Shell.Current.GoToAsync(AppRoutes.TripTimeline);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_groupId))
                {
                    StatusText = "Trip not found.";
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
            "Export this trip",
            "Cancel",
            null,
            "JSON snapshot",
            "CSV spreadsheet",
            "PDF summary");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var format = choice switch
        {
            "JSON snapshot" => (ExportFormat?)ExportFormat.Json,
            "CSV spreadsheet" => ExportFormat.Csv,
            "PDF summary" => ExportFormat.Pdf,
            _ => (ExportFormat?)null
        };

        if (format is null) return;

        try
        {
            var result = await _dataService.ExportTripAsync(_groupId, format.Value);
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Export trip",
                File = new ShareFile(result.FilePath, result.MimeType)
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed. {ex.Message}";
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public sealed record TripPersonEditorViewModel(string Name, string HouseholdText, bool CanRemove);
}