using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ArchivedTripsPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<TripListItemModel> Trips { get; } = new();

    public ArchivedTripsPage(AppDataService dataService)
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
        var trips = await _dataService.GetArchivedTripsAsync();

        Trips.Clear();
        foreach (var trip in trips)
            Trips.Add(trip);
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    // Navigate to the trip timeline in read-only (archived) mode.
    // We pass the groupId as a query param so TripPage loads that specific trip
    // WITHOUT changing the user's currently selected active trip.
    private async void OnViewTripClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await Shell.Current.GoToAsync($"{AppRoutes.TripTimeline}?groupId={Uri.EscapeDataString(groupId)}");
        }
    }
}
