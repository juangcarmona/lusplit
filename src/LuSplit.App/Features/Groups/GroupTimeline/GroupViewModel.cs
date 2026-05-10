using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Services;
using LuSplit.App.Services.Presentation;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Features.Groups.GroupTimeline;

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly IGroupPageDataService _dataService;
    private readonly SyncOrchestrationService? _syncOrchestration;
    private readonly IAuthPort? _authPort;
    private string? _overrideGroupId;
    private string? _currentGroupId;
    private string? _ownerId;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInviteAction))]
    [NotifyPropertyChangedFor(nameof(ShowMembersAction))]
    [NotifyPropertyChangedFor(nameof(ShowOwnerActions))]
    [NotifyPropertyChangedFor(nameof(SharedEmptyStateHint))]
    private bool _isShared;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInviteAction))]
    [NotifyPropertyChangedFor(nameof(ShowOwnerActions))]
    [NotifyPropertyChangedFor(nameof(SharedEmptyStateHint))]
    private bool _isCurrentUserOwner;

    public bool ShowInviteAction => IsShared && IsCurrentUserOwner;
    public bool ShowMembersAction => IsShared;
    public bool ShowOwnerActions => IsShared && IsCurrentUserOwner;

    /// <summary>Contextual hint shown in the empty state for shared groups.</summary>
    public string? SharedEmptyStateHint => IsShared
        ? (IsCurrentUserOwner
            ? "Invite people to start splitting expenses together."
            : "Waiting for expenses. The group owner or other members will add them.")
        : null;

    public ObservableCollection<TimelineEntryViewModel> TimelineItems { get; } = new();
    public ObservableCollection<BalanceLineViewModel> BalanceLines { get; } = new();

    public event EventHandler<string?>? GroupDetailsRequested;
    public event EventHandler? SettleUpRequested;
    public event EventHandler? AddExpenseRequested;
    public event EventHandler? RecordPaymentRequested;
    public event EventHandler<string>? ExportRequested;
    public event EventHandler<string>? InviteRequested;
    public event EventHandler<string>? MembersRequested;

    public GroupViewModel(IGroupPageDataService dataService, SyncOrchestrationService? syncOrchestration = null, IAuthPort? authPort = null)
    {
        _dataService = dataService;
        _syncOrchestration = syncOrchestration;
        _authPort = authPort;
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
        _ownerId = workspace.OwnerId;

        IsShared = workspace.IsShared;
        if (workspace.IsShared && _authPort is not null)
        {
            var currentUserId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);
            IsCurrentUserOwner = currentUserId is not null &&
                                 string.Equals(currentUserId, workspace.OwnerId, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            IsCurrentUserOwner = false;
        }

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

    [RelayCommand]
    private void NavigateToInvite()
    {
        var groupId = _overrideGroupId ?? _currentGroupId;
        if (groupId is not null && IsShared && IsCurrentUserOwner)
            InviteRequested?.Invoke(this, groupId);
    }

    [RelayCommand]
    private void NavigateToMembers()
    {
        var groupId = _overrideGroupId ?? _currentGroupId;
        if (groupId is not null && IsShared)
            MembersRequested?.Invoke(this, groupId);
    }
}
