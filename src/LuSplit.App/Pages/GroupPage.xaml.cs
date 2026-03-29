using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class GroupPage : ContentPage, IQueryAttributable
{
    private readonly GroupViewModel _viewModel;
    private readonly AppDataService _dataService;

    public GroupPage(AppDataService dataService)
    {
        _dataService = dataService;
        _viewModel = new GroupViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;

        dataService.DataChanged += async (_, _) =>
            await MainThread.InvokeOnMainThreadAsync(_viewModel.HandleDataChangedAsync);

        _viewModel.GroupDetailsRequested += OnGroupDetailsRequested;
        _viewModel.SettleUpRequested += OnSettleUpRequested;
        _viewModel.AddExpenseRequested += OnAddExpenseRequested;
        _viewModel.RecordPaymentRequested += OnRecordPaymentRequested;
        _viewModel.ExportRequested += OnExportRequested;
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
}
