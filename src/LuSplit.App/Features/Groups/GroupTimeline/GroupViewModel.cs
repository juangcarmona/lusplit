using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Services;
using LuSplit.App.Services.Presentation;

namespace LuSplit.App.Features.Groups.GroupTimeline;

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly IGroupPageDataService _dataService;
    private readonly SyncOrchestrationService? _syncOrchestration;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isReadOnly;

    [ObservableProperty] private string? _accessRemovedMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncStatusText))]
    [NotifyPropertyChangedFor(nameof(ShowSyncIndicator))]
    private SyncState _groupSyncState = SyncState.Unknown;

    public bool ShowSyncIndicator => GroupSyncState != SyncState.Unknown;

    public string SyncStatusText => GroupSyncState switch
    {
        SyncState.UpToDate => "Up to date",
        SyncState.Syncing => "Syncing…",
        SyncState.Error => "Sync error",
        _ => string.Empty
    };

    // Aliases used by SyncStatusIndicator control
    public string StatusText => SyncStatusText;
    public string StatusIconGlyph => GroupSyncState switch
    {
        SyncState.UpToDate => "\uf00c",
        SyncState.Syncing => "\uf021",
        SyncState.Error => "\uf071",
        _ => string.Empty
    };

    public bool HasGroupImage => !string.IsNullOrWhiteSpace(GroupImagePath);
    public bool HasNoGroupImage => !HasGroupImage;
    public bool CanEdit => !IsArchived && !IsReadOnly;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();
    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public event EventHandler<string?>? GroupDetailsRequested;
    public event EventHandler? SettleUpRequested;
    public event EventHandler? AddExpenseRequested;
    public event EventHandler? RecordPaymentRequested;
    public event EventHandler<string>? ExportRequested;

    public GroupViewModel(IGroupPageDataService dataService, SyncOrchestrationService? syncOrchestration = null)
    {
        _dataService = dataService;
        _syncOrchestration = syncOrchestration;
        if (_syncOrchestration is not null)
            _syncOrchestration.SyncStateChanged += OnSyncStateChanged;
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
        var wasReadOnly = IsReadOnly;
        IsReadOnly = workspace.IsReadOnly;
        if (workspace.IsReadOnly && !wasReadOnly)
            AccessRemovedMessage = "You no longer have access to this group.";
        GroupImagePath = workspace.ImagePath;
        _currentGroupId = workspace.GroupId;

        if (_syncOrchestration is not null && _currentGroupId is not null)
            GroupSyncState = _syncOrchestration.GetState(_currentGroupId);

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

    private void OnSyncStateChanged(object? sender, SyncStateChangedArgs e)
    {
        if (string.Equals(e.GroupId, _currentGroupId, StringComparison.Ordinal))
            GroupSyncState = e.State;
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
