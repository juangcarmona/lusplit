using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Errors;
using LuSplit.App.Services.Persistence;
using LuSplit.App.Services.Presentation;

namespace LuSplit.App.Features.Home.Home;

public sealed partial class HomeViewModel : ObservableObject
{
    private enum WorkspaceTab { Overview, Expenses, Balances }

    private readonly IHomeDataService _dataService;
    private WorkspaceTab _selectedTab = WorkspaceTab.Overview;
    private const int RecentItemsCount = 5;

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> RecentEvents { get; } = new();
    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoGroupsEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowAddExpenseButton))]
    [NotifyPropertyChangedFor(nameof(ShowSettleUpButton))]
    private bool _hasGroup = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupImage))]
    [NotifyPropertyChangedFor(nameof(HasNoGroupImage))]
    private string? _groupImagePath;

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private string _groupMetaText = string.Empty;

    [ObservableProperty]
    private string _totalUnsettledText = string.Empty;

    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;

    public bool ShowNoGroupsEmptyState => !HasGroup;
    public bool ShowOverview => _selectedTab == WorkspaceTab.Overview;
    public bool ShowExpenses => _selectedTab == WorkspaceTab.Expenses;
    public bool ShowBalances => _selectedTab == WorkspaceTab.Balances;
    public bool HasEvents => Events.Count > 0;
    public bool ShowWhoOwesWhatSection => WhoOwesWho.Count > 0;
    public bool ShowBalancesSection => Balances.Count > 0;
    public bool ShowOverviewEmptyState => ShowOverview && !HasEvents;
    public bool ShowExpensesEmptyState => ShowExpenses && !HasEvents;
    public bool ShowBalancesEmptyState => ShowBalances && !ShowWhoOwesWhatSection && !ShowBalancesSection;
    public bool ShowAddExpenseButton => HasGroup && _selectedTab != WorkspaceTab.Balances;
    public bool ShowSettleUpButton => HasGroup && _selectedTab == WorkspaceTab.Balances;

    /// <summary>Raised whenever the selected tab changes. Code-behind uses this to update tab button styles.</summary>
    public event EventHandler? TabChanged;

    public HomeViewModel(IHomeDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadAsync()
    {
        GroupWorkspaceModel workspace;
        try
        {
            workspace = await _dataService.GetGroupWorkspaceAsync();
        }
        catch (NoGroupsAvailableException)
        {
            HasGroup = false;
            GroupName = string.Empty;
            GroupMetaText = string.Empty;
            TotalUnsettledText = string.Empty;
            GroupImagePath = null;
            Balances.Clear();
            Events.Clear();
            RecentEvents.Clear();
            WhoOwesWho.Clear();
            NotifyCollectionDerivedState();
            NotifyTabState();
            return;
        }
        catch (Exception ex)
        {
            // An unexpected error (e.g. data-integrity violation) must not crash the
            // app via async-void OnAppearing. Surface it as status text so the user
            // can still navigate away or switch groups.
            GroupName = string.Empty;
            GroupMetaText = ex.Message;
            TotalUnsettledText = string.Empty;
            GroupImagePath = null;
            Balances.Clear();
            Events.Clear();
            RecentEvents.Clear();
            WhoOwesWho.Clear();
            NotifyCollectionDerivedState();
            NotifyTabState();
            return;
        }

        HasGroup = true;
        var overview = workspace.Overview;
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);
        var whoOwesWho = GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode);

        GroupName = workspace.GroupName;
        GroupImagePath = workspace.ImagePath;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        TotalUnsettledText = whoOwesWho.Count == 0
            ? AppResources.Home_AllSettled
            : string.Format(AppResources.Home_UnsettledFormat, GroupPresentationMapper.FormatTotalUnsettled(overview));

        Balances.Clear();
        foreach (var line in GroupPresentationMapper.BuildNetBalances(overview, settlementMode))
            Balances.Add(new HomeBalanceRowViewModel(line.ParticipantId, line.Name, line.AmountText, line.IsPositive));

        Events.Clear();
        foreach (var item in GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons))
            Events.Add(item);

        RecentEvents.Clear();
        foreach (var item in Events.Take(RecentItemsCount))
            RecentEvents.Add(item);

        WhoOwesWho.Clear();
        foreach (var line in whoOwesWho)
            WhoOwesWho.Add(line);

        NotifyCollectionDerivedState();
        NotifyTabState();
    }

    [RelayCommand]
    private void SelectOverviewTab() => SetSelectedTab(WorkspaceTab.Overview);

    [RelayCommand]
    private void SelectExpensesTab() => SetSelectedTab(WorkspaceTab.Expenses);

    [RelayCommand]
    private void SelectBalancesTab() => SetSelectedTab(WorkspaceTab.Balances);

    private void SetSelectedTab(WorkspaceTab tab)
    {
        if (_selectedTab == tab) return;
        _selectedTab = tab;
        NotifyTabState();
        TabChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyCollectionDerivedState()
    {
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(ShowWhoOwesWhatSection));
        OnPropertyChanged(nameof(ShowBalancesSection));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
        OnPropertyChanged(nameof(ShowAddExpenseButton));
        OnPropertyChanged(nameof(ShowSettleUpButton));
    }

    private void NotifyTabState()
    {
        OnPropertyChanged(nameof(ShowOverview));
        OnPropertyChanged(nameof(ShowExpenses));
        OnPropertyChanged(nameof(ShowBalances));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
        OnPropertyChanged(nameof(ShowAddExpenseButton));
        OnPropertyChanged(nameof(ShowSettleUpButton));
    }
}
