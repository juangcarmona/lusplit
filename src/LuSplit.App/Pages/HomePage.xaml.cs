using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<TripListItemModel> Trips { get; } = new();

    public HomePage(AppDataService dataService)
    {
        _dataService = dataService;

        InitializeComponent();
        BindingContext = this;

        _dataService.DataChanged += OnDataChanged;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
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
            Trips.Add(trip);
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnOpenTripClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await _dataService.SelectTripAsync(groupId);
            await Shell.Current.GoToAsync(AppRoutes.TripTimeline);
        }
    }

    private async void OnEditTripClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await _dataService.SelectTripAsync(groupId);
            await Shell.Current.GoToAsync(AppRoutes.TripDetails);
        }
    }

    private async void OnNewTripClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{AppRoutes.TripDetails}?mode=create");
    }
}
