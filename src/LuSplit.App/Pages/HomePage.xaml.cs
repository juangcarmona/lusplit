using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;
    private string? _currentTripId;

    public ObservableCollection<TripListItemModel> Trips { get; } = new();

    public string CurrentTripName { get; private set; } = "Trips";

    public string CurrentTripSummaryText { get; private set; } = "Create a trip to get started.";

    public string CurrentTripBalanceText { get; private set; } = string.Empty;

    public bool HasCurrentTrip { get; private set; }

    public HomePage(AppDataService dataService)
    {
        _dataService = dataService;

        InitializeComponent();
        BindingContext = this;

        _dataService.DataChanged += OnDataChanged;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
        System.Diagnostics.Debug.WriteLine($"Banner ID assigned: {BottomBanner.AdsId}");
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var trips = await _dataService.GetTripsAsync();

        Trips.Clear();
        foreach (var trip in trips)
        {
            Trips.Add(trip);
        }

        var currentTrip = trips.FirstOrDefault(trip => trip.IsCurrent) ?? trips.FirstOrDefault();
        _currentTripId = currentTrip?.GroupId;
        HasCurrentTrip = currentTrip is not null;
        CurrentTripName = currentTrip?.Name ?? "Trips";
        CurrentTripSummaryText = currentTrip?.SummaryText ?? "Create a trip to get started.";
        CurrentTripBalanceText = currentTrip?.BalancePreviewText ?? string.Empty;

        OnPropertyChanged(nameof(CurrentTripName));
        OnPropertyChanged(nameof(CurrentTripSummaryText));
        OnPropertyChanged(nameof(CurrentTripBalanceText));
        OnPropertyChanged(nameof(HasCurrentTrip));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnOpenCurrentTripClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentTripId))
        {
            await OpenTripAsync(_currentTripId);
        }
    }

    private async void OnEditCurrentTripClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentTripId))
        {
            await EditTripAsync(_currentTripId);
        }
    }

    private async void OnOpenTripClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await OpenTripAsync(groupId);
        }
    }

    private async void OnEditTripClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await EditTripAsync(groupId);
        }
    }

    private async void OnNewTripClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{AppRoutes.TripDetails}?mode=create");
    }

    private async Task OpenTripAsync(string groupId)
    {
        await _dataService.SelectTripAsync(groupId);
        await Shell.Current.GoToAsync(AppRoutes.TripTimeline);
    }

    private async Task EditTripAsync(string groupId)
    {
        await _dataService.SelectTripAsync(groupId);
        await Shell.Current.GoToAsync(AppRoutes.TripDetails);
    }
}
