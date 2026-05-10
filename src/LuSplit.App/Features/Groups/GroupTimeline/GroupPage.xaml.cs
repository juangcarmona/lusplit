using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.App.Services.Export;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Features.Groups.GroupTimeline;

public partial class GroupPage : ContentPage, IQueryAttributable
{
    private readonly GroupViewModel _viewModel;
    private readonly AppDataService _dataService;
    private ToolbarItem? _inviteToolbarItem;
    private ToolbarItem? _membersToolbarItem;

    public GroupPage(AppDataService dataService, SyncOrchestrationService syncOrchestration, IAuthPort? authPort = null)
    {
        _dataService = dataService;
        _viewModel = new GroupViewModel(dataService, syncOrchestration, authPort);
        InitializeComponent();
        BindingContext = _viewModel;

        dataService.DataChanged += async (_, _) =>
            await MainThread.InvokeOnMainThreadAsync(_viewModel.HandleDataChangedAsync);

        _viewModel.GroupDetailsRequested += OnGroupDetailsRequested;
        _viewModel.SettleUpRequested += OnSettleUpRequested;
        _viewModel.AddExpenseRequested += OnAddExpenseRequested;
        _viewModel.RecordPaymentRequested += OnRecordPaymentRequested;
        _viewModel.ExportRequested += OnExportRequested;
        _viewModel.InviteRequested += OnInviteRequested;
        _viewModel.MembersRequested += OnMembersRequested;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = query.TryGetValue("groupId", out var v) && !string.IsNullOrWhiteSpace(v?.ToString())
            ? v.ToString() : null;
        _viewModel.SetOverrideGroupId(id);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        UpdateSharedToolbarItems();
    }

    private void UpdateSharedToolbarItems()
    {
        // Remove existing dynamic items
        if (_inviteToolbarItem is not null)
        {
            ToolbarItems.Remove(_inviteToolbarItem);
            _inviteToolbarItem = null;
        }
        if (_membersToolbarItem is not null)
        {
            ToolbarItems.Remove(_membersToolbarItem);
            _membersToolbarItem = null;
        }

        // Add Invite item for shared-group owners
        if (_viewModel.ShowInviteAction)
        {
            _inviteToolbarItem = new ToolbarItem
            {
                Text = "Invite",
                Command = _viewModel.NavigateToInviteCommand,
                Order = ToolbarItemOrder.Secondary
            };
            ToolbarItems.Insert(0, _inviteToolbarItem);
        }

        // Add Members item for all shared groups
        if (_viewModel.ShowMembersAction)
        {
            _membersToolbarItem = new ToolbarItem
            {
                Text = "Members",
                Command = _viewModel.NavigateToMembersCommand,
                Order = ToolbarItemOrder.Secondary
            };
            ToolbarItems.Insert(_inviteToolbarItem is not null ? 1 : 0, _membersToolbarItem);
        }
    }

    private async void OnGroupDetailsRequested(object? sender, string? overrideGroupId)
    {
        if (overrideGroupId is not null)
            await Shell.Current.GoToAsync($"{AppRoutes.GroupDetails}?groupId={Uri.EscapeDataString(overrideGroupId)}");
        else
            await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
    }

    private async void OnSettleUpRequested(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Settlement);

    private async void OnAddExpenseRequested(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.AddExpense);

    private async void OnRecordPaymentRequested(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.RecordPayment);

    private async void OnExportRequested(object? sender, string groupId)
    {
        try
        {
            await GroupExportService.RunExportFlowAsync(this, _dataService, groupId);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(null, string.Format(AppResources.Export_Failed, ex.Message), AppResources.Common_Ok);
        }
    }

    private async void OnInviteRequested(object? sender, string groupId)
        => await Shell.Current.GoToAsync($"{AppRoutes.Invite}?groupId={groupId}");

    private async void OnMembersRequested(object? sender, string groupId)
        => await Shell.Current.GoToAsync($"{AppRoutes.MemberList}?groupId={groupId}");
}
