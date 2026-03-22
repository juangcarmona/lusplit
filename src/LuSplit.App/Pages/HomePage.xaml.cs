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
    public bool HasEvents => Events.Count > 0;
    public bool ShowWhoOwesWhatSection => WhoOwesWho.Count > 0;
    public bool ShowBalancesSection => Balances.Count > 0;
    public bool ShowOverviewEmptyState => ShowOverview && !HasEvents;
    public bool ShowExpensesEmptyState => ShowExpenses && !HasEvents;
    public bool ShowBalancesEmptyState => ShowBalances && !ShowWhoOwesWhatSection && !ShowBalancesSection;

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
        await EnsureStartupProfileAsync();
        await LoadAsync();
    }

    private async Task EnsureStartupProfileAsync()
    {
        AppPreferences.InitializePreferredCurrencyIfNeeded();

        if (!string.IsNullOrWhiteSpace(UserProfilePreferences.GetPreferredName())
            || UserProfilePreferences.HasSeenPreferredNamePrompt())
        {
            return;
        }

        var preferredName = await DisplayPromptAsync(
            AppResources.Settings_Title,
            AppResources.Settings_ProfileHint,
            AppResources.Common_Ok,
            AppResources.Common_Cancel,
            AppResources.Settings_MyNamePlaceholder,
            maxLength: 60);

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            UserProfilePreferences.SetPreferredName(preferredName);
        }

        UserProfilePreferences.MarkPreferredNamePromptSeen();
    }

    private async Task LoadAsync()
    {
        GroupWorkspaceModel workspace;
        try
        {
            workspace = await _dataService.GetGroupWorkspaceAsync();
        }
        catch (NoGroupsAvailableException)
        {
            await Shell.Current.GoToAsync(AppRoutes.CreateGroup);
            return;
        }

        var overview = workspace.Overview;

        GroupName = workspace.GroupName;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);
        var whoOwesWho = GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode);
        TotalUnsettledText = whoOwesWho.Count == 0
            ? AppResources.Home_AllSettled
            : string.Format(
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
        foreach (var line in whoOwesWho)
        {
            WhoOwesWho.Add(line);
        }

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupMetaText));
        OnPropertyChanged(nameof(TotalUnsettledText));
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(ShowWhoOwesWhatSection));
        OnPropertyChanged(nameof(ShowBalancesSection));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
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
        await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
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

    private async void OnEventSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView)
        {
            return;
        }

        if (e.CurrentSelection.FirstOrDefault() is not CompactEventEntryViewModel selected
            || !selected.IsExpense
            || string.IsNullOrWhiteSpace(selected.SourceId))
        {
            collectionView.SelectedItem = null;
            return;
        }

        await Shell.Current.GoToAsync($"{AppRoutes.ExpenseDetails}?expenseId={Uri.EscapeDataString(selected.SourceId)}");
        collectionView.SelectedItem = null;
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
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));

        var unselectedStyle = (Style)MauiApplication.Current!.Resources["SecondaryButton"];

        OverviewTabButton.Style = _selectedTab == WorkspaceTab.Overview ? null : unselectedStyle;
        ExpensesTabButton.Style = _selectedTab == WorkspaceTab.Expenses ? null : unselectedStyle;
        BalancesTabButton.Style = _selectedTab == WorkspaceTab.Balances ? null : unselectedStyle;
    }
}

public sealed record HomeBalanceRowViewModel(string ParticipantId, string Name, string AmountText, Color AmountColor);
