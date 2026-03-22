using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using MauiApplication = Microsoft.Maui.Controls.Application;

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
    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public string PreferredName { get; set; } = string.Empty;
    public CurrencyOption? SelectedCurrencyOption { get; set; }
    public bool IsDarkThemeEnabled { get; set; }
    public bool ShowProfileTab => _selectedTab == SettingsTab.Profile;
    public bool ShowLanguageTab => _selectedTab == SettingsTab.Language;

    public LanguageSettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        PreferredName = UserProfilePreferences.GetPreferredName();
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        BuildLanguageList();
        ApplyTabVisualState();
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        UserProfilePreferences.SetPreferredName(PreferredName);
        AppPreferences.SetPreferredCurrency(SelectedCurrencyOption?.Code);
        AppPreferences.SetDarkThemeEnabled(IsDarkThemeEnabled);
        PreferredName = UserProfilePreferences.GetPreferredName();
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        OnPropertyChanged(nameof(PreferredName));
        OnPropertyChanged(nameof(SelectedCurrencyOption));
        OnPropertyChanged(nameof(IsDarkThemeEnabled));
        await DisplayAlert(AppResources.Settings_Title, AppResources.Settings_ProfileSaved, AppResources.Common_Cancel);
    }

    private void BuildCurrencyList(string preferredCurrencyCode)
    {
        CurrencyOptions.Clear();
        foreach (var option in CurrencyCatalog.GetSupportedCurrencyOptions())
        {
            CurrencyOptions.Add(option);
        }

        SelectedCurrencyOption = CurrencyCatalog.FindByCode(CurrencyOptions, preferredCurrencyCode)
            ?? CurrencyCatalog.FindByCode(CurrencyOptions, CurrencyCatalog.DefaultCurrencyCode);
        OnPropertyChanged(nameof(SelectedCurrencyOption));
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
            Languages.Add(new LanguageOptionViewModel(
                option.Culture,
                option.DisplayLabel,
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

        var unselectedStyle = (Style)MauiApplication.Current!.Resources["SecondaryButton"];
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
