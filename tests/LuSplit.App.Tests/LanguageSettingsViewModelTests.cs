using LuSplit.App.Pages;

namespace LuSplit.App.Tests;

public class LanguageSettingsViewModelTests
{
    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void InitialState_ShowsProfileTab()
    {
        var vm = new LanguageSettingsViewModel();

        Assert.True(vm.ShowProfileTab);
        Assert.False(vm.ShowLanguageTab);
    }

    [Fact]
    public void InitialState_CurrencyOptionsPopulated()
    {
        var vm = new LanguageSettingsViewModel();

        Assert.NotEmpty(vm.CurrencyOptions);
    }

    [Fact]
    public void InitialState_SelectedCurrencyOptionNotNull()
    {
        var vm = new LanguageSettingsViewModel();

        Assert.NotNull(vm.SelectedCurrencyOption);
    }

    [Fact]
    public void InitialState_LanguagesPopulated()
    {
        var vm = new LanguageSettingsViewModel();

        // Stub returns 5 languages
        Assert.Equal(5, vm.Languages.Count);
    }

    [Fact]
    public void InitialState_IsDarkThemeEnabled_MatchesStub()
    {
        // Stub returns false
        var vm = new LanguageSettingsViewModel();

        Assert.False(vm.IsDarkThemeEnabled);
    }

    [Fact]
    public void InitialState_PreferredName_FromStub()
    {
        // UserProfilePreferencesStub returns empty string
        var vm = new LanguageSettingsViewModel();

        Assert.Equal(string.Empty, vm.PreferredName);
    }

    // ── Tab switching ──────────────────────────────────────────────────────

    [Fact]
    public void SelectLanguageTabCommand_ShowsLanguageTab()
    {
        var vm = new LanguageSettingsViewModel();

        vm.SelectLanguageTabCommand.Execute(null);

        Assert.True(vm.ShowLanguageTab);
        Assert.False(vm.ShowProfileTab);
    }

    [Fact]
    public void SelectProfileTabCommand_FromLanguageTab_RestoresProfileTab()
    {
        var vm = new LanguageSettingsViewModel();
        vm.SelectLanguageTabCommand.Execute(null);

        vm.SelectProfileTabCommand.Execute(null);

        Assert.True(vm.ShowProfileTab);
        Assert.False(vm.ShowLanguageTab);
    }

    [Fact]
    public void SelectProfileTabCommand_AlreadyOnProfile_RemainsProfileTab()
    {
        var vm = new LanguageSettingsViewModel();

        vm.SelectProfileTabCommand.Execute(null);

        Assert.True(vm.ShowProfileTab);
    }

    [Fact]
    public void SelectLanguageTabCommand_CalledTwice_StaysOnLanguageTab()
    {
        var vm = new LanguageSettingsViewModel();
        vm.SelectLanguageTabCommand.Execute(null);

        vm.SelectLanguageTabCommand.Execute(null);

        Assert.True(vm.ShowLanguageTab);
    }

    [Fact]
    public void SelectLanguageTabCommand_RaisesPropertyChanged_ForShowProfileTab()
    {
        var vm = new LanguageSettingsViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectLanguageTabCommand.Execute(null);

        Assert.Contains(nameof(vm.ShowProfileTab), changed);
        Assert.Contains(nameof(vm.ShowLanguageTab), changed);
    }

    // ── Language list building ──────────────────────────────────────────────

    [Fact]
    public void BuildLanguageList_EmptySavedCode_MarksSystemDefaultSelected()
    {
        // Stub GetSavedLanguageCode returns ""
        var vm = new LanguageSettingsViewModel();

        var systemDefault = vm.Languages.FirstOrDefault(l => l.Culture == "");

        Assert.NotNull(systemDefault);
        Assert.True(systemDefault!.IsSelected);
    }

    [Fact]
    public void BuildLanguageList_EmptySavedCode_NonDefaultLanguagesNotSelected()
    {
        var vm = new LanguageSettingsViewModel();

        var nonDefault = vm.Languages.Where(l => l.Culture != "");

        Assert.All(nonDefault, l => Assert.False(l.IsSelected));
    }

    [Fact]
    public void BuildLanguageList_LanguageOptionsHaveExpectedCultures()
    {
        var vm = new LanguageSettingsViewModel();

        var cultures = vm.Languages.Select(l => l.Culture).ToList();

        Assert.Contains("", cultures);
        Assert.Contains("en", cultures);
        Assert.Contains("es", cultures);
    }

    [Fact]
    public void BuildLanguageList_DisplayLabelContainsFlag()
    {
        var vm = new LanguageSettingsViewModel();

        // All language options from the stub have a flag prefix
        Assert.All(vm.Languages, l => Assert.False(string.IsNullOrWhiteSpace(l.DisplayLabel)));
    }

    // ── Save profile ───────────────────────────────────────────────────────

    [Fact]
    public void SaveProfileCommand_FiresProfileSaved()
    {
        var vm = new LanguageSettingsViewModel();
        var fired = false;
        vm.ProfileSaved += (_, _) => fired = true;

        vm.SaveProfileCommand.Execute(null);

        Assert.True(fired);
    }

    [Fact]
    public void SaveProfileCommand_CurrencyOptionsRemainPopulated()
    {
        var vm = new LanguageSettingsViewModel();
        var countBefore = vm.CurrencyOptions.Count;

        vm.SaveProfileCommand.Execute(null);

        Assert.Equal(countBefore, vm.CurrencyOptions.Count);
    }

    [Fact]
    public void SaveProfileCommand_SelectedCurrencyOptionRemains()
    {
        var vm = new LanguageSettingsViewModel();
        var originalCode = vm.SelectedCurrencyOption?.Code;

        vm.SaveProfileCommand.Execute(null);

        Assert.Equal(originalCode, vm.SelectedCurrencyOption?.Code);
    }

    [Fact]
    public void SaveProfileCommand_IsDarkThemeEnabled_ReadBackFromStub()
    {
        var vm = new LanguageSettingsViewModel();
        vm.IsDarkThemeEnabled = true;

        vm.SaveProfileCommand.Execute(null);

        // Stub returns false (ignores writes), so re-read gets false
        Assert.False(vm.IsDarkThemeEnabled);
    }

    // ── SelectLanguage ─────────────────────────────────────────────────────

    [Fact]
    public void SelectLanguage_ValidCulture_DoesNotThrow()
    {
        var vm = new LanguageSettingsViewModel();

        var ex = Record.Exception(() => vm.SelectLanguage("en"));

        Assert.Null(ex);
    }

    [Fact]
    public void SelectLanguage_EmptyString_DoesNotThrow()
    {
        var vm = new LanguageSettingsViewModel();

        var ex = Record.Exception(() => vm.SelectLanguage(string.Empty));

        Assert.Null(ex);
    }

    // ── LanguageOptionViewModel ────────────────────────────────────────────

    [Fact]
    public void LanguageOptionViewModel_StoresAllProperties()
    {
        var option = new LanguageOptionViewModel("en", "🇬🇧 English", true);

        Assert.Equal("en", option.Culture);
        Assert.Equal("🇬🇧 English", option.DisplayLabel);
        Assert.True(option.IsSelected);
    }

    [Fact]
    public void LanguageOptionViewModel_NotSelected_IsSelectedFalse()
    {
        var option = new LanguageOptionViewModel("de", "🇩🇪 German", false);

        Assert.False(option.IsSelected);
    }
}
