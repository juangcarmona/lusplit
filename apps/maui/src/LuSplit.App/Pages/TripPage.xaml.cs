using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class TripPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();

    public string TripName { get; private set; } = "Trip";

    public string TripSummaryText { get; private set; } = "0 people ready to go";

    public string TripBalancePreviewText { get; private set; } = "Everyone is even right now.";

    public TripPage(AppDataService dataService)
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
        var workspace = await _dataService.GetTripWorkspaceAsync();

        TripName = workspace.TripName;
        TripSummaryText = TripPresentationMapper.BuildTripSummary(workspace.Overview);
        TripBalancePreviewText = TripPresentationMapper.BuildBalancePreview(workspace.Overview, 1).FirstOrDefault() ?? "Everyone is even right now.";
        Title = workspace.TripName;

        TimelineItems.Clear();
        foreach (var item in TripPresentationMapper.BuildTimeline(workspace.Overview, workspace.ExpenseIcons))
        {
            TimelineItems.Add(item);
        }

        OnPropertyChanged(nameof(TripName));
        OnPropertyChanged(nameof(TripSummaryText));
        OnPropertyChanged(nameof(TripBalancePreviewText));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnTripDetailsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.TripDetails);
    }

    private async void OnBackToTripsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddEvent);
    }
}