using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ActivityPage : ContentPage
{
    private readonly ActivityViewModel _viewModel;

    public ActivityPage(AppDataService dataService)
    {
        _viewModel = new ActivityViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;

        dataService.DataChanged += async (_, _) =>
            await MainThread.InvokeOnMainThreadAsync(_viewModel.HandleDataChangedAsync);
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
