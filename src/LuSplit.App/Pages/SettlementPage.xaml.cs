using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class SettlementPage : ContentPage
{
    private readonly AppDataService _dataService;
    private string _currency = "USD";

    public ObservableCollection<SettlementSuggestionRowViewModel> WhoOwesWho { get; } = new();

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
        _currency = overview.Group.Currency;

        WhoOwesWho.Clear();
        foreach (var suggestion in GroupPresentationMapper.BuildSettlementSuggestions(overview))
        {
            WhoOwesWho.Add(new SettlementSuggestionRowViewModel(
                suggestion.FromParticipantId,
                suggestion.ToParticipantId,
                suggestion.AmountMinor,
                suggestion.Text,
                suggestion.AmountText));
        }
    }

    private async void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TapGestureRecognizer { BindingContext: SettlementSuggestionRowViewModel row })
        {
            return;
        }

        await Shell.Current.GoToAsync(
            $"{AppRoutes.RecordPayment}?payerId={Uri.EscapeDataString(row.PayerId)}&receiverId={Uri.EscapeDataString(row.ReceiverId)}&amountMinor={row.AmountMinor}&currency={Uri.EscapeDataString(_currency)}");
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

}

public sealed record SettlementSuggestionRowViewModel(
    string PayerId,
    string ReceiverId,
    long AmountMinor,
    string Text,
    string AmountText);
