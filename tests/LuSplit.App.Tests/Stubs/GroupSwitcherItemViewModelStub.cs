using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Pages;

/// <summary>
/// Test stub for GroupSwitcherItemViewModel. Strips MAUI ImageSource and File.Exists dependencies
/// while preserving the pure API surface used by GroupSwitcherViewModel.
/// </summary>
public sealed class GroupSwitcherItemViewModel
{
    public string GroupId { get; }
    public string Name { get; }
    public bool IsCurrent { get; }
    public string? ImagePath { get; }

    public bool CanSelect => !IsCurrent;
    public string DisplayName => IsCurrent ? $"{Name} {AppResources.GroupSwitcher_CurrentSuffix}" : Name;
    public string AvatarInitial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    public bool HasImage => false;
    public bool HasNoImage => true;

    public GroupSwitcherItemViewModel(string groupId, string name, bool isCurrent, string? imagePath = null)
    {
        GroupId = groupId;
        Name = name;
        IsCurrent = isCurrent;
        ImagePath = imagePath;
    }
}
