using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class SettlementPage : ContentPage
{
    private readonly AppDataService _dataService;
    private SettlementMode _mode = SettlementMode.Participant;

    public ObservableCollection<BalanceItemViewModel> Balances { get; } = new();

    public ObservableCollection<TransferItemViewModel> Transfers { get; } = new();

    public SettlementPage(AppDataService dataService)
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
        var result = await _dataService.GetSettlementAsync(_mode);

        Balances.Clear();
        foreach (var balance in result.Balances)
        {
            Balances.Add(new BalanceItemViewModel(balance.EntityId, FormatMinor(balance.AmountMinor)));
        }

        Transfers.Clear();
        foreach (var transfer in result.Settlement.Transfers)
        {
            Transfers.Add(new TransferItemViewModel($"{transfer.FromParticipantId} owes {transfer.ToParticipantId} {FormatMinor(transfer.AmountMinor)}"));
        }
    }

    private async void OnParticipantModeClicked(object? sender, EventArgs e)
    {
        _mode = SettlementMode.Participant;
        await LoadAsync();
    }

    private async void OnOwnerModeClicked(object? sender, EventArgs e)
    {
        _mode = SettlementMode.EconomicUnitOwner;
        await LoadAsync();
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private static string FormatMinor(long minor)
        => string.Create(CultureInfo.InvariantCulture, $"${minor / 100.0:0.00}");

    public sealed record BalanceItemViewModel(string EntityId, string Amount);

    public sealed record TransferItemViewModel(string Text);
}
