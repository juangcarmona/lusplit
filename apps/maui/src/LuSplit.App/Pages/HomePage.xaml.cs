using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();

    public ObservableCollection<string> BalancePreview { get; } = new();

    public string TripHeaderText { get; private set; } = "Current trip";

    public string TripSummaryText { get; private set; } = "0 people ready to go";

    public HomePage(AppDataService dataService)
    {
        _dataService = dataService;

        InitializeComponent();
        BindingContext = this;

        _dataService.DataChanged += OnDataChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var overview = await _dataService.GetOverviewAsync();

        TripHeaderText = "Current trip";
        TripSummaryText = TripPresentationMapper.BuildTripSummary(overview);

        BalancePreview.Clear();
        foreach (var line in TripPresentationMapper.BuildBalancePreview(overview, 2))
        {
            BalancePreview.Add(line);
        }

        TimelineItems.Clear();
        foreach (var item in TripPresentationMapper.BuildTimeline(overview))
        {
            TimelineItems.Add(item);
        }

        OnPropertyChanged(nameof(TripHeaderText));
        OnPropertyChanged(nameof(TripSummaryText));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddEvent);
    }
}
