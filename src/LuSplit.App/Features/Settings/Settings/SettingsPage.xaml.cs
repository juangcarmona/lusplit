namespace LuSplit.App.Features.Settings.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage()
    {
        _viewModel = new SettingsViewModel();
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.NavigateToAuthRequested += OnNavigateToAuth;
        _viewModel.NavigateToDevicesRequested += OnNavigateToDevices;
    }

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string culture) return;
        _viewModel.SelectLanguage(culture);
    }

    private async void OnNavigateToAuth(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.Authentication);
        _viewModel.RefreshAccountState();
    }

    private async void OnNavigateToDevices(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.DeviceManagement);
}
