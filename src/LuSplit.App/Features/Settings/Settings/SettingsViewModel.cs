using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Services;
using LuSplit.App.Services.Formatting;
using LuSplit.App.Services.Localization;
using LuSplit.App.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace LuSplit.App.Features.Settings.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private bool _isProfileTabSelected = true;
    private readonly SessionService? _session;

    [ObservableProperty] private string _preferredName = string.Empty;
    [ObservableProperty] private CurrencyOption? _selectedCurrencyOption;
    [ObservableProperty] private bool _isDarkThemeEnabled;

    // Linked-account state — forwarded from SessionService.
    public bool IsSignedIn => _session?.IsSignedIn ?? LinkedAccountStore.HasLinkedAccount;
    public string? AccountDisplayName => _session?.DisplayName ?? LinkedAccountStore.DisplayName;
    public string? AccountUsername => _session?.Username ?? LinkedAccountStore.Username;

    public event EventHandler? ProfileSaved;

    /// <summary>Raised when the user taps the Account/Auth row.</summary>
    public event EventHandler? NavigateToAuthRequested;

    /// <summary>Raised when the user taps the Devices row.</summary>
    public event EventHandler? NavigateToDevicesRequested;

    public bool ShowProfileTab => _isProfileTabSelected;
    public bool ShowLanguageTab => !_isProfileTabSelected;

    /// <summary>
    /// Display version of the installed app, sourced from package metadata at runtime.
    /// Set by the pipeline via <c>ApplicationDisplayVersion</c> (e.g. "1.0.18").
    /// </summary>
    public string AppVersion { get; } =
        $"{AppInfoProvider.VersionString} ({AppInfoProvider.BuildString})";

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();
    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = new();

    public SettingsViewModel()
    {
        PreferredName = UserProfilePreferences.GetPreferredName();
        IsDarkThemeEnabled = AppPreferences.IsDarkThemeEnabled();
        BuildCurrencyList(AppPreferences.GetPreferredCurrency());
        BuildLanguageList();

        // Resolve singleton SessionService for live auth state updates.
        _session = App.Services?.GetService<SessionService>();
        if (_session is not null)
        {
            _session.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(SessionService.IsSignedIn)
                    or nameof(SessionService.DisplayName)
                    or nameof(SessionService.Username))
                {
                    RefreshAccountState();
                }
            };
        }
    }

    /// <summary>
    /// Raises property-changed notifications for account state so the Settings UI updates.
    /// </summary>
    public void RefreshAccountState()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(AccountDisplayName));
        OnPropertyChanged(nameof(AccountUsername));
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

    [RelayCommand]
    private void NavigateToAuth()
        => NavigateToAuthRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void NavigateToDevices()
        => NavigateToDevicesRequested?.Invoke(this, EventArgs.Empty);
}
