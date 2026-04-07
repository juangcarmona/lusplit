using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Features.Settings.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage()
    {
        _viewModel = new SettingsViewModel();
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.ProfileSaved += OnProfileSaved;
    }

    private async void OnProfileSaved(object? sender, EventArgs e)
        => await DisplayAlert(AppResources.Settings_Title, AppResources.Settings_ProfileSaved, AppResources.Common_Cancel);

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string culture) return;
        _viewModel.SelectLanguage(culture);
    }
}
