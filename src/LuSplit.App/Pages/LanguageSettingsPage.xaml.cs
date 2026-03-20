using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class LanguageSettingsPage : ContentPage
{
    private enum SettingsTab
    {
        Profile,
        Language
    }

    private SettingsTab _selectedTab = SettingsTab.Profile;

    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = new();
    public ObservableCollection<string> CurrencyOptions { get; } = new() { "USD", "EUR", "GBP" };
    public string PreferredName { get; set; } = string.Empty;
    public string? SelectedCurrency { get; set; } = "USD";
    public bool IsDarkThemeEnabled { get; set; }
    public bool ShowProfileTab => _selectedTab == SettingsTab.Profile;
    public bool ShowLanguageTab => _selectedTab == SettingsTab.Language;

    public LanguageSettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        PreferredName = UserProfilePreferences.GetPreferredName();
        SelectedCurrency = AppPreferences.GetPreferredCurrency();
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        BuildLanguageList();
        ApplyTabVisualState();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        UserProfilePreferences.SetPreferredName(PreferredName);
        AppPreferences.SetPreferredCurrency(SelectedCurrency);
        AppPreferences.SetDarkThemeEnabled(IsDarkThemeEnabled);
        PreferredName = UserProfilePreferences.GetPreferredName();
        SelectedCurrency = AppPreferences.GetPreferredCurrency();
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        OnPropertyChanged(nameof(PreferredName));
        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(IsDarkThemeEnabled));
        await DisplayAlert(AppResources.Settings_Title, AppResources.Settings_ProfileSaved, AppResources.Common_Cancel);
    }

    private void OnProfileTabClicked(object? sender, EventArgs e)
    {
        SetSelectedTab(SettingsTab.Profile);
    }

    private void OnLanguageTabClicked(object? sender, EventArgs e)
    {
        SetSelectedTab(SettingsTab.Language);
    }

    private void BuildLanguageList()
    {
        var saved = LocalizationHelper.GetSavedLanguageCode();

        foreach (var option in LocalizationHelper.SupportedLanguages)
        {
            // The "System Default" entry uses a resource key so its label follows the active language.
            var displayLabel = string.IsNullOrEmpty(option.Culture)
                ? $"{option.Flag} {AppResources.Language_SystemDefault}"
                : option.DisplayLabel;

            Languages.Add(new LanguageOptionViewModel(
                option.Culture,
                displayLabel,
                string.Equals(option.Culture, saved, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string culture) return;

        LocalizationHelper.SetAndApplyLanguage(culture);
        // UI is rebuilt by SetAndApplyLanguage; no further action needed here.
    }

    private void SetSelectedTab(SettingsTab tab)
    {
        if (_selectedTab == tab)
        {
            return;
        }

        _selectedTab = tab;
        ApplyTabVisualState();
    }

    private void ApplyTabVisualState()
    {
        OnPropertyChanged(nameof(ShowProfileTab));
        OnPropertyChanged(nameof(ShowLanguageTab));

        var unselectedStyle = (Style)Application.Current!.Resources["SecondaryButton"];
        ProfileTabButton.Style = _selectedTab == SettingsTab.Profile ? null : unselectedStyle;
        LanguageTabButton.Style = _selectedTab == SettingsTab.Language ? null : unselectedStyle;
    }

    public sealed class LanguageOptionViewModel
    {
        public string Culture { get; }
        public string DisplayLabel { get; }
        public bool IsSelected { get; }

        public LanguageOptionViewModel(string culture, string displayLabel, bool isSelected)
        {
            Culture = culture;
            DisplayLabel = displayLabel;
            IsSelected = isSelected;
        }
    }
}
