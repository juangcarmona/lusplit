using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class TripPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    // Set when navigating to this page for a specific (e.g. archived) trip,
    // without changing the user's currently selected active trip.
    private string? _overrideGroupId;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();

    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public string TripName { get; private set; } = "Trip";

    public string TripSummaryText { get; private set; } = string.Empty;

    public bool IsArchived { get; private set; }

    public bool CanEdit => !IsArchived;

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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _overrideGroupId = query.TryGetValue("groupId", out var id) && !string.IsNullOrWhiteSpace(id?.ToString())
            ? id.ToString()
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var workspace = _overrideGroupId is not null
            ? await _dataService.GetTripWorkspaceAsync(_overrideGroupId)
            : await _dataService.GetTripWorkspaceAsync();

        TripName = workspace.TripName;
        TripSummaryText = TripPresentationMapper.BuildTripSummary(workspace.Overview);
        IsArchived = workspace.Overview.Group.Closed;
        Title = workspace.TripName;

        TimelineItems.Clear();
        foreach (var item in TripPresentationMapper.BuildTimeline(workspace.Overview, workspace.ExpenseIcons))
            TimelineItems.Add(item);

        BalanceLines.Clear();
        var settlementMode = TripPresentationMapper.ResolveSettlementMode(workspace.Overview);
        foreach (var line in TripPresentationMapper.BuildWhoOwesWho(workspace.Overview, settlementMode))
            BalanceLines.Add(line);

        OnPropertyChanged(nameof(TripName));
        OnPropertyChanged(nameof(TripSummaryText));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        // Only refresh if we are showing the currently selected trip.
        if (_overrideGroupId is null)
        {
            await MainThread.InvokeOnMainThreadAsync(LoadAsync);
        }
    }

    private async void OnTripDetailsClicked(object? sender, EventArgs e)
    {
        if (_overrideGroupId is not null)
        {
            await Shell.Current.GoToAsync($"{AppRoutes.TripDetails}?groupId={Uri.EscapeDataString(_overrideGroupId)}");
        }
        else
        {
            await Shell.Current.GoToAsync(AppRoutes.TripDetails);
        }
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
