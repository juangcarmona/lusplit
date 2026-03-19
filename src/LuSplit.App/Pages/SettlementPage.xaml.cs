using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class SettlementPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    public SettlementPage(AppDataService dataService)
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
        var overview = await _dataService.GetOverviewAsync();

        WhoOwesWho.Clear();
        foreach (var line in GroupPresentationMapper.BuildWhoOwesWho(overview, SettlementMode.Participant))
        {
            WhoOwesWho.Add(line);
        }
    }

    private async void OnRecordPaymentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.RecordPayment);
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }
}
