using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public sealed partial class LanguageSettingsViewModel : ObservableObject
{
    private bool _isProfileTabSelected = true;

    [ObservableProperty] private string _preferredName = string.Empty;
    [ObservableProperty] private CurrencyOption? _selectedCurrencyOption;
    [ObservableProperty] private bool _isDarkThemeEnabled;

    public bool ShowProfileTab => _isProfileTabSelected;
    public bool ShowLanguageTab => !_isProfileTabSelected;

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = new();

    public event EventHandler? ProfileSaved;

    public LanguageSettingsViewModel()
    {
        PreferredName = UserProfilePreferences.GetPreferredName();
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());
        BuildLanguageList();
    }

    [RelayCommand]
    private void SelectProfileTab()
    {
        if (_isProfileTabSelected) return;
        _isProfileTabSelected = true;
        OnPropertyChanged(nameof(ShowProfileTab));
        OnPropertyChanged(nameof(ShowLanguageTab));
    }

    [RelayCommand]
    private void SelectLanguageTab()
    {
        if (_isProfileTabSelected is false) return;
        _isProfileTabSelected = false;
        OnPropertyChanged(nameof(ShowProfileTab));
        OnPropertyChanged(nameof(ShowLanguageTab));
    }

    [RelayCommand]
    private void SaveProfile()
    {
        UserProfilePreferences.SetPreferredName(PreferredName);
        AppPreferences.SetPreferredCurrency(SelectedCurrencyOption?.Code);
        AppPreferences.SetDarkThemeEnabled(IsDarkThemeEnabled);

        PreferredName = UserProfilePreferences.GetPreferredName();
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());

        ProfileSaved?.Invoke(this, EventArgs.Empty);
    }

    public void SelectLanguage(string culture)
    {
        LocalizationHelper.SetAndApplyLanguage(culture);
    }

    private void BuildCurrencyList(string preferredCurrencyCode)
    {
        CurrencyOptions.Clear();
        CurrencyCatalog.PopulateSupportedOptions(CurrencyOptions);

        SelectedCurrencyOption = CurrencyCatalog.FindByCode(CurrencyOptions, preferredCurrencyCode)
            ?? CurrencyCatalog.FindByCode(CurrencyOptions, CurrencyCatalog.DefaultCurrencyCode);
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
