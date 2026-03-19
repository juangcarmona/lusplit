using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<HomeSettlementRowViewModel> SettlementSuggestions { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();

    public string GroupName { get; private set; } = string.Empty;
    public string GroupMetaText { get; private set; } = string.Empty;
    public string TotalUnsettledText { get; private set; } = string.Empty;

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
        var workspace = await _dataService.GetGroupWorkspaceAsync();
        var overview = workspace.Overview;

        GroupName = workspace.GroupName;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        TotalUnsettledText = $"Unsettled: {GroupPresentationMapper.FormatTotalUnsettled(overview)}";

        Balances.Clear();
        foreach (var line in GroupPresentationMapper.BuildNetBalances(overview))
        {
            Balances.Add(new HomeBalanceRowViewModel(
                line.ParticipantId,
                line.Name,
                line.AmountText,
                line.IsPositive
                    ? (Color)Application.Current!.Resources["PositiveSoftGreen"]
                    : (Color)Application.Current!.Resources["ErrorSoftRed"]));
        }

        SettlementSuggestions.Clear();
        foreach (var suggestion in GroupPresentationMapper.BuildSettlementSuggestions(overview))
        {
            SettlementSuggestions.Add(new HomeSettlementRowViewModel(
                suggestion.Text,
                suggestion.AmountText,
                $"payerId={Uri.EscapeDataString(suggestion.FromParticipantId)}&receiverId={Uri.EscapeDataString(suggestion.ToParticipantId)}&amountMinor={suggestion.AmountMinor}"));
        }

        Events.Clear();
        foreach (var item in GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons))
        {
            Events.Add(item);
        }

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupMetaText));
        OnPropertyChanged(nameof(TotalUnsettledText));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnAddExpenseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddEvent);
    }

    private async void OnEditGroupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
    }

    private async void OnPayClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.RecordPayment);
    }

    private async void OnViewArchivedClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.ArchivedGroups);
    }

    private async void OnPaySuggestionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string query } && !string.IsNullOrWhiteSpace(query))
        {
            await Shell.Current.GoToAsync($"{AppRoutes.RecordPayment}?{query}");
        }
    }
}

public sealed record HomeBalanceRowViewModel(string ParticipantId, string Name, string AmountText, Color AmountColor);

public sealed record HomeSettlementRowViewModel(string Text, string AmountText, string CommandParameter);
