using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class TripPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();

    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public string TripName { get; private set; } = "Trip";

    public string TripSummaryText { get; private set; } = string.Empty;

    public TripPage(AppDataService dataService)
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
        var workspace = await _dataService.GetTripWorkspaceAsync();

        TripName = workspace.TripName;
        TripSummaryText = TripPresentationMapper.BuildTripSummary(workspace.Overview);
        Title = workspace.TripName;

        TimelineItems.Clear();
        foreach (var item in TripPresentationMapper.BuildTimeline(workspace.Overview, workspace.ExpenseIcons))
            TimelineItems.Add(item);

        BalanceLines.Clear();
        foreach (var line in TripPresentationMapper.BuildWhoOwesWho(workspace.Overview, SettlementMode.Participant))
            BalanceLines.Add(line);

        OnPropertyChanged(nameof(TripName));
        OnPropertyChanged(nameof(TripSummaryText));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnTripDetailsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.TripDetails);
    }

    private async void OnSettleUpClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.Settlement);
    }

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddEvent);
    }

    private async void OnRecordPaymentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.RecordPayment);
    }
}