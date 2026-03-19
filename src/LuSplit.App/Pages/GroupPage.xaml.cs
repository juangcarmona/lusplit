using System.Collections.ObjectModel;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class GroupPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    // Set when navigating to this page for a specific (e.g. archived) group,
    // without changing the user's currently selected active group.
    private string? _overrideGroupId;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();

    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public string GroupName { get; private set; } = string.Empty;

    public string GroupSummaryText { get; private set; } = string.Empty;

    public bool IsArchived { get; private set; }

    public bool CanEdit => !IsArchived;

    public GroupPage(AppDataService dataService)
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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var workspace = _overrideGroupId is not null
            ? await _dataService.GetGroupWorkspaceAsync(_overrideGroupId)
            : await _dataService.GetGroupWorkspaceAsync();

        GroupName = workspace.GroupName;
        GroupSummaryText = GroupPresentationMapper.BuildGroupSummary(workspace.Overview);
        IsArchived = workspace.Overview.Group.Closed;
        Title = workspace.GroupName;

        TimelineItems.Clear();
        foreach (var item in GroupPresentationMapper.BuildTimeline(workspace.Overview, workspace.ExpenseIcons))
            TimelineItems.Add(item);

        BalanceLines.Clear();
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(workspace.Overview);
        foreach (var line in GroupPresentationMapper.BuildWhoOwesWho(workspace.Overview, settlementMode))
            BalanceLines.Add(line);

        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(GroupSummaryText));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(CanEdit));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        // Only refresh if we are showing the currently selected group.
        if (_overrideGroupId is null)
        {
            await MainThread.InvokeOnMainThreadAsync(LoadAsync);
        }
    }

    private async void OnGroupDetailsClicked(object? sender, EventArgs e)
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

    private async void OnSettleUpClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.Settlement);
    }

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.AddEvent);
    }

    private async void OnRecordPaymentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.RecordPayment);
    }
}
