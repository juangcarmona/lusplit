namespace LuSplit.App.Features.Settings.Settings
{
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
