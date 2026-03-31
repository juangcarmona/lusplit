using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Features.Settings.LanguageSettings;

public partial class LanguageSettingsPage : ContentPage
{
    private readonly LanguageSettingsViewModel _viewModel;

    public LanguageSettingsPage()
    {
        _viewModel = new LanguageSettingsViewModel();
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

