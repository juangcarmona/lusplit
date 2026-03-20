using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.App.Resources.Localization;
using LuSplit.Application.Models;

using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private enum WorkspaceTab
    {
        Overview,
        Expenses,
        Balances
    }

    private readonly AppDataService _dataService;
    private WorkspaceTab _selectedTab = WorkspaceTab.Overview;

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    public string GroupName { get; private set; } = string.Empty;
    public string GroupMetaText { get; private set; } = string.Empty;
    public string TotalUnsettledText { get; private set; } = string.Empty;
    public bool ShowOverview => _selectedTab == WorkspaceTab.Overview;
    public bool ShowExpenses => _selectedTab == WorkspaceTab.Expenses;
    public bool ShowBalances => _selectedTab == WorkspaceTab.Balances;

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
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);
        TotalUnsettledText = string.Format(
            AppResources.Home_UnsettledFormat,
            GroupPresentationMapper.FormatTotalUnsettled(overview));

        Balances.Clear();
        foreach (var line in GroupPresentationMapper.BuildNetBalances(overview, settlementMode))
        {
            Balances.Add(new HomeBalanceRowViewModel(
                line.ParticipantId,
                line.Name,
                line.AmountText,
                line.IsPositive
                    ? (Color)MauiApplication.Current!.Resources["PositiveSoftGreen"]
                    : (Color)MauiApplication.Current!.Resources["ErrorSoftRed"]));
        }

        Events.Clear();
        foreach (var item in GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons))
        {
            Events.Add(item);
        }

        WhoOwesWho.Clear();
        foreach (var line in GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode))
        {
            WhoOwesWho.Add(line);
        }

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupMetaText));
        OnPropertyChanged(nameof(TotalUnsettledText));
        ApplyTabVisualState();
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnAddExpenseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddExpense);
    }

    private void OnOpenDrawerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnOverflowClicked(object? sender, EventArgs e)
    {
        var editGroup = AppResources.Group_EditGroup;
        var settleUp = AppResources.Group_SettleUp;
        var export = AppResources.GroupDetails_ExportButton;
        var archive = AppResources.GroupDetails_ArchiveButton;
        var cancel = AppResources.Common_Cancel;

        var selected = await DisplayActionSheet(AppResources.Group_OverflowLabel, cancel, null, editGroup, settleUp, export, archive);
        if (string.Equals(selected, editGroup, StringComparison.Ordinal))
        {
            await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
        }
        else if (string.Equals(selected, settleUp, StringComparison.Ordinal))
        {
            await Shell.Current.GoToAsync(AppRoutes.Settlement);
        }
        else if (string.Equals(selected, export, StringComparison.Ordinal) || string.Equals(selected, archive, StringComparison.Ordinal))
        {
            await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
        }
    }

    private void OnExpensesTabClicked(object? sender, EventArgs e)
    {
        SetSelectedTab(WorkspaceTab.Expenses);
    }

    private void OnBalancesTabClicked(object? sender, EventArgs e)
    {
        SetSelectedTab(WorkspaceTab.Balances);
    }

    private void OnOverviewTabClicked(object? sender, EventArgs e)
    {
        SetSelectedTab(WorkspaceTab.Overview);
    }

    private async void OnSettleUpClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.Settlement);
    }

    private void SetSelectedTab(WorkspaceTab tab)
    {
        if (_selectedTab == tab)
        {
            return;
        }

        _selectedTab = tab;
        ApplyTabVisualState();
    }

    private void ApplyTabVisualState()
    {
        OnPropertyChanged(nameof(ShowOverview));
        OnPropertyChanged(nameof(ShowExpenses));
        OnPropertyChanged(nameof(ShowBalances));

        var unselectedStyle = (Style)MauiApplication.Current!.Resources["SecondaryButton"];

        OverviewTabButton.Style = _selectedTab == WorkspaceTab.Overview ? null : unselectedStyle;
        ExpensesTabButton.Style = _selectedTab == WorkspaceTab.Expenses ? null : unselectedStyle;
        BalancesTabButton.Style = _selectedTab == WorkspaceTab.Balances ? null : unselectedStyle;
    }
}

public sealed record HomeBalanceRowViewModel(string ParticipantId, string Name, string AmountText, Color AmountColor);
