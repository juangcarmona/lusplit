using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class LanguageSettingsPage : ContentPage
{
    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = new();

    public LanguageSettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        BuildLanguageList();
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
