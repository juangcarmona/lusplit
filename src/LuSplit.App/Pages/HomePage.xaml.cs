using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.App.Resources.Localization;
using LuSplit.Application.Models;

using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage, IQueryAttributable
{
    private enum WorkspaceTab
    {
        Overview,
        Expenses,
        Balances
    }

    private readonly AppDataService _dataService;
    private WorkspaceTab _selectedTab = WorkspaceTab.Overview;
    // Set when navigating to this page for a specific archived group, without
    // changing the user's currently selected active group.
    private string? _overrideGroupId;
    // Guards against flyout-tap navigation (which skips ApplyQueryAttributes)
    // restoring a stale archived override.
    private bool _pendingQueryApplied;

    private const int RecentItemsCount = 5;

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> RecentEvents { get; } = new();
    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    public bool HasGroup { get; private set; } = true;
    public bool IsArchived { get; private set; }

    public string? GroupImagePath { get; private set; }
    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;
    public ImageSource? GroupImageSource =>
        HasGroupImage ? ImageSource.FromFile(GroupImagePath!) : null;

    public string GroupName { get; private set; } = string.Empty;
    public string GroupMetaText { get; private set; } = string.Empty;
    public string TotalUnsettledText { get; private set; } = string.Empty;

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
    public bool ShowAddExpenseButton => HasGroup && !IsArchived && _selectedTab != WorkspaceTab.Balances;
    public bool ShowSettleUpButton => HasGroup && !IsArchived && _selectedTab == WorkspaceTab.Balances;

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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _overrideGroupId = query.TryGetValue("groupId", out var id) && !string.IsNullOrWhiteSpace(id?.ToString())
            ? id.ToString()
            : null;
        _pendingQueryApplied = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // If OnAppearing fires without a preceding ApplyQueryAttributes call
        // (flyout-item tap), clear any stale archived override.
        if (!_pendingQueryApplied)
        {
            _overrideGroupId = null;
        }
        _pendingQueryApplied = false;

        if (_overrideGroupId is null)
        {
            await EnsureStartupProfileAsync();
        }
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
            workspace = _overrideGroupId is not null
                ? await _dataService.GetGroupWorkspaceAsync(_overrideGroupId)
                : await _dataService.GetGroupWorkspaceAsync();
        }
        catch (NoGroupsAvailableException)
        {
            HasGroup = false;
            IsArchived = false;
            GroupName = string.Empty;
            GroupMetaText = string.Empty;
            TotalUnsettledText = string.Empty;
            GroupImagePath = null;

            Balances.Clear();
            Events.Clear();
            RecentEvents.Clear();
            WhoOwesWho.Clear();

            NotifyWorkspaceStateChanged();
            ApplyTabVisualState();
            return;
        }

        HasGroup = true;
        IsArchived = workspace.Overview.Group.Closed;

        var overview = workspace.Overview;
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);
        var whoOwesWho = GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode);

        GroupName = workspace.GroupName;
        GroupImagePath = workspace.ImagePath;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
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

        RecentEvents.Clear();
        foreach (var item in Events.Take(RecentItemsCount))
        {
            RecentEvents.Add(item);
        }

        WhoOwesWho.Clear();
        foreach (var line in whoOwesWho)
        {
            WhoOwesWho.Add(line);
        }

        NotifyWorkspaceStateChanged();
        ApplyTabVisualState();
    }

    private void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(HasGroup));
        OnPropertyChanged(nameof(ShowNoGroupsEmptyState));
        OnPropertyChanged(nameof(IsArchived));

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupMetaText));
        OnPropertyChanged(nameof(TotalUnsettledText));

        OnPropertyChanged(nameof(GroupImagePath));
        OnPropertyChanged(nameof(HasGroupImage));
        OnPropertyChanged(nameof(HasNoGroupImage));
        OnPropertyChanged(nameof(GroupImageSource));

        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(ShowWhoOwesWhatSection));
        OnPropertyChanged(nameof(ShowBalancesSection));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
        OnPropertyChanged(nameof(ShowAddExpenseButton));
        OnPropertyChanged(nameof(ShowSettleUpButton));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        // Skip refresh when showing a specific archived group override.
        if (_overrideGroupId is not null) return;
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnAddExpenseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddExpense);
    }

    private async void OnCreateGroupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.CreateGroup);
    }

    private void OnOpenDrawerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnOverflowClicked(object? sender, EventArgs e)
    {
        if (_overrideGroupId is not null)
        {
            await Shell.Current.GoToAsync($"{AppRoutes.GroupDetails}?groupId={Uri.EscapeDataString(_overrideGroupId)}");
        }
        else
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
        OnPropertyChanged(nameof(ShowAddExpenseButton));
        OnPropertyChanged(nameof(ShowSettleUpButton));

        var unselectedStyle = (Style)MauiApplication.Current!.Resources["SecondaryButton"];

        OverviewTabButton.Style = _selectedTab == WorkspaceTab.Overview ? null : unselectedStyle;
        ExpensesTabButton.Style = _selectedTab == WorkspaceTab.Expenses ? null : unselectedStyle;
        BalancesTabButton.Style = _selectedTab == WorkspaceTab.Balances ? null : unselectedStyle;
    }
}

public sealed record HomeBalanceRowViewModel(string ParticipantId, string Name, string AmountText, Color AmountColor);