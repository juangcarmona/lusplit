using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Services.Presentation;

namespace LuSplit.App.Pages;

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly IGroupPageDataService _dataService;
    private string? _overrideGroupId;
    private string? _currentGroupId;

    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private string _groupSummaryText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupImage))]
    [NotifyPropertyChangedFor(nameof(HasNoGroupImage))]
    private string? _groupImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isArchived;

    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;
    public bool CanEdit => !IsArchived;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();
    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public event EventHandler<string?>? GroupDetailsRequested;
    public event EventHandler? SettleUpRequested;
    public event EventHandler? AddExpenseRequested;
    public event EventHandler? RecordPaymentRequested;
    public event EventHandler<string>? ExportRequested;

    public GroupViewModel(IGroupPageDataService dataService)
    {
        _dataService = dataService;
    }

    public void SetOverrideGroupId(string? groupId)
    {
        _overrideGroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
    }

    public async Task LoadAsync()
    {
        var workspace = _overrideGroupId is not null
            ? await _dataService.GetGroupWorkspaceAsync(_overrideGroupId)
            : await _dataService.GetGroupWorkspaceAsync();

        GroupName = workspace.GroupName;
        GroupSummaryText = GroupPresentationMapper.BuildGroupSummary(workspace.Overview);
        IsArchived = workspace.Overview.Group.Closed;
        GroupImagePath = workspace.ImagePath;
        _currentGroupId = workspace.GroupId;

        TimelineItems.Clear();
        foreach (var item in GroupPresentationMapper.BuildTimeline(workspace.Overview, workspace.ExpenseIcons))
            TimelineItems.Add(item);

        BalanceLines.Clear();
        var settlementMode = GroupPresentationMapper.ResolveSettlementMode(workspace.Overview);
        foreach (var line in GroupPresentationMapper.BuildWhoOwesWho(workspace.Overview, settlementMode))
            BalanceLines.Add(line);
    }

    /// <summary>Encapsulates reload logic for DataChanged; call via MainThread.InvokeOnMainThreadAsync from code-behind.</summary>
    public async Task HandleDataChangedAsync()
    {
        if (_overrideGroupId is null)
            await LoadAsync();
    }

    [RelayCommand]
    private void NavigateToGroupDetails()
        => GroupDetailsRequested?.Invoke(this, _overrideGroupId);

    [RelayCommand]
    private void NavigateToSettleUp()
        => SettleUpRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void NavigateToAddExpense()
        => AddExpenseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void NavigateToRecordPayment()
        => RecordPaymentRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestExport()
    {
        var groupId = _overrideGroupId ?? _currentGroupId;
        if (groupId is not null)
            ExportRequested?.Invoke(this, groupId);
    }
}
