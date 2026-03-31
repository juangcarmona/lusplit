using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Pages;

public partial class GroupSwitcherPage : ContentPage
{
    private readonly GroupSwitcherViewModel _viewModel;

    public GroupSwitcherPage(AppDataService dataService)
    {
        _viewModel = new GroupSwitcherViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.GroupSelected += OnGroupSelected;
        _viewModel.NewGroupRequested += OnNewGroupRequested;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnGroupSelected(object? sender, string groupId)
        => await Shell.Current.GoToAsync($"//{AppRoutes.Home}");

    private async void OnNewGroupRequested(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.CreateGroup);
}

