using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public sealed partial class ArchivedGroupViewModel : ObservableObject
{
    private readonly IArchivedGroupDataService _dataService;
    private string _groupId = string.Empty;
    private const int RecentItemsCount = 5;

    internal enum Tab { Overview, Expenses, Balances }

    // ── Collections ───────────────────────────────────────────────────────

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> RecentEvents { get; } = new();
    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    // ── State ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupImage))]
    [NotifyPropertyChangedFor(nameof(HasNoGroupImage))]
    private string? _groupImagePath;

    [ObservableProperty]
    private string _groupMetaText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWhoOwesWhatSection))]
    [NotifyPropertyChangedFor(nameof(ShowBalancesSection))]
    [NotifyPropertyChangedFor(nameof(ShowBalancesEmptyState))]
    private bool _hasWhoOwesWho;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWhoOwesWhatSection))]
    [NotifyPropertyChangedFor(nameof(ShowBalancesSection))]
    [NotifyPropertyChangedFor(nameof(ShowBalancesEmptyState))]
    private bool _hasBalances;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    [NotifyPropertyChangedFor(nameof(ShowOverviewEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowExpensesEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowBalancesEmptyState))]
    private int _eventCount;

    private Tab _activeTab = Tab.Overview;

    // ── Derived — image ───────────────────────────────────────────────────

    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;

    // ── Derived — tab visibility ──────────────────────────────────────────

    public bool ShowOverview => _activeTab == Tab.Overview;
    public bool ShowExpenses => _activeTab == Tab.Expenses;
    public bool ShowBalances => _activeTab == Tab.Balances;

    public bool IsOverviewTab => _activeTab == Tab.Overview;
    public bool IsExpensesTab => _activeTab == Tab.Expenses;
    public bool IsBalancesTab => _activeTab == Tab.Balances;

    // ── Derived — section/empty-state visibility ──────────────────────────

    public bool HasEvents => EventCount > 0;
    public bool ShowWhoOwesWhatSection => HasWhoOwesWho;
    public bool ShowBalancesSection => HasBalances;

    public bool ShowOverviewEmptyState => ShowOverview && !HasEvents;
    public bool ShowExpensesEmptyState => ShowExpenses && !HasEvents;
    public bool ShowBalancesEmptyState => ShowBalances && !ShowWhoOwesWhatSection && !ShowBalancesSection;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Code-behind calls <see cref="GroupExportService"/> when this fires.</summary>
    public event EventHandler<string>? ExportRequested;

    // ── Constructor ───────────────────────────────────────────────────────

    public ArchivedGroupViewModel(IArchivedGroupDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>Call before <see cref="LoadAsync"/> so the page knows which group to show.</summary>
    public void PrepareForGroup(string groupId) => _groupId = groupId;

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        var workspace = await _dataService.GetGroupWorkspaceAsync(_groupId);
        var overview = workspace.Overview;
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);

        GroupName = workspace.GroupName;
        GroupImagePath = workspace.ImagePath;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);

        var balanceLines = GroupPresentationMapper.BuildNetBalances(overview, settlementMode).ToList();
        Balances.Clear();
        foreach (var line in balanceLines)
            Balances.Add(new HomeBalanceRowViewModel(line.ParticipantId, line.Name, line.AmountText, line.IsPositive));

        var compactEvents = GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons).ToList();
        Events.Clear();
        foreach (var item in compactEvents)
            Events.Add(item);

        RecentEvents.Clear();
        foreach (var item in compactEvents.Take(RecentItemsCount))
            RecentEvents.Add(item);

        var whoOwesWhoLines = GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode).ToList();
        WhoOwesWho.Clear();
        foreach (var line in whoOwesWhoLines)
            WhoOwesWho.Add(line);

        HasWhoOwesWho = whoOwesWhoLines.Count > 0;
        HasBalances = balanceLines.Count > 0;
        EventCount = compactEvents.Count;
    }

    [RelayCommand]
    private void SelectOverviewTab() => SetTab(Tab.Overview);

    [RelayCommand]
    private void SelectExpensesTab() => SetTab(Tab.Expenses);

    [RelayCommand]
    private void SelectBalancesTab() => SetTab(Tab.Balances);

    [RelayCommand]
    private void RequestExport()
    {
        if (!string.IsNullOrWhiteSpace(_groupId))
            ExportRequested?.Invoke(this, _groupId);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void SetTab(Tab tab)
    {
        if (_activeTab == tab) return;
        _activeTab = tab;
        NotifyTabChanged();
    }

    private void NotifyTabChanged()
    {
        OnPropertyChanged(nameof(ShowOverview));
        OnPropertyChanged(nameof(ShowExpenses));
        OnPropertyChanged(nameof(ShowBalances));
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsExpensesTab));
        OnPropertyChanged(nameof(IsBalancesTab));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
    }
}
