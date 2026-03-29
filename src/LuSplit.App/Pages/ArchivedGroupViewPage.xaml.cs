using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Pages;

public partial class ArchivedGroupViewPage : ContentPage
{
    private enum WorkspaceTab { Overview, Expenses, Balances }

    private const int RecentItemsCount = 5;

    private readonly AppDataService _dataService;
    private WorkspaceTab _selectedTab = WorkspaceTab.Overview;
    private string _groupId = string.Empty;

    public ObservableCollection<HomeBalanceRowViewModel> Balances { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
    public ObservableCollection<CompactEventEntryViewModel> RecentEvents { get; } = new();
    public ObservableCollection<BalanceLineViewModel> WhoOwesWho { get; } = new();

    public string GroupName { get; private set; } = string.Empty;
    public string? GroupImagePath { get; private set; }
    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;
    public ImageSource? GroupImageSource =>
        HasGroupImage ? ImageSource.FromFile(GroupImagePath!) : null;
    public string GroupMetaText { get; private set; } = string.Empty;

    public bool ShowOverview => _selectedTab == WorkspaceTab.Overview;
    public bool ShowExpenses => _selectedTab == WorkspaceTab.Expenses;
    public bool ShowBalances => _selectedTab == WorkspaceTab.Balances;
    public bool HasEvents => Events.Count > 0;
    public bool ShowWhoOwesWhatSection => WhoOwesWho.Count > 0;
    public bool ShowBalancesSection => Balances.Count > 0;
    public bool ShowOverviewEmptyState => ShowOverview && !HasEvents;
    public bool ShowExpensesEmptyState => ShowExpenses && !HasEvents;
    public bool ShowBalancesEmptyState => ShowBalances && !ShowWhoOwesWhatSection && !ShowBalancesSection;

    public ArchivedGroupViewPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    /// <summary>Must be called before Navigation.PushAsync so LoadAsync has the group ID.</summary>
    public void PrepareForGroup(string groupId)
    {
        _groupId = groupId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var workspace = await _dataService.GetGroupWorkspaceAsync(_groupId);
        var overview = workspace.Overview;
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(overview);
        var whoOwesWho = GroupPresentationMapper.BuildWhoOwesWho(overview, settlementMode);

        GroupName = workspace.GroupName;
        GroupImagePath = workspace.ImagePath;
        GroupMetaText = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        Title = workspace.GroupName;

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

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupImagePath));
        OnPropertyChanged(nameof(HasGroupImage));
        OnPropertyChanged(nameof(HasNoGroupImage));
        OnPropertyChanged(nameof(GroupImageSource));
        OnPropertyChanged(nameof(GroupMetaText));
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(ShowWhoOwesWhatSection));
        OnPropertyChanged(nameof(ShowBalancesSection));
        OnPropertyChanged(nameof(ShowOverviewEmptyState));
        OnPropertyChanged(nameof(ShowExpensesEmptyState));
        OnPropertyChanged(nameof(ShowBalancesEmptyState));
    }

    private void OnOverviewTabClicked(object? sender, EventArgs e) => SetSelectedTab(WorkspaceTab.Overview);
    private void OnExpensesTabClicked(object? sender, EventArgs e) => SetSelectedTab(WorkspaceTab.Expenses);
    private void OnBalancesTabClicked(object? sender, EventArgs e) => SetSelectedTab(WorkspaceTab.Balances);

    private void SetSelectedTab(WorkspaceTab tab)
    {
        if (_selectedTab == tab) return;
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

    private async void OnEventSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView) return;

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

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_groupId)) return;
        try
        {
            await GroupExportService.RunExportFlowAsync(this, _dataService, _groupId);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(null, string.Format(AppResources.Export_Failed, ex.Message), AppResources.Common_Ok);
        }
    }
}
