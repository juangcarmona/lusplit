using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class LanguageSettingsPage : ContentPage
{
    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = new();
    public string PreferredName { get; set; } = string.Empty;

    public LanguageSettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        PreferredName = UserProfilePreferences.GetPreferredName();
        BuildLanguageList();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        UserProfilePreferences.SetPreferredName(PreferredName);
        PreferredName = UserProfilePreferences.GetPreferredName();
        OnPropertyChanged(nameof(PreferredName));
        await DisplayAlert(AppResources.Settings_Title, AppResources.Settings_ProfileSaved, AppResources.Common_Cancel);
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
