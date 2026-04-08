namespace LuSplit.App.Features.Settings.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage()
    {
        _viewModel = new SettingsViewModel();
        InitializeComponent();
        BindingContext = _viewModel;
    }

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string culture) return;
        _viewModel.SelectLanguage(culture);
    }
}
