using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Features.Groups.GroupSwitcher;

public sealed class GroupSwitcherItemViewModel
{
    public string GroupId { get; }
    public string Name { get; }
    public bool IsCurrent { get; }
    public string? ImagePath { get; }

    public bool CanSelect => !IsCurrent;
    public string DisplayName => IsCurrent ? $"{Name} {AppResources.GroupSwitcher_CurrentSuffix}" : Name;
    public string AvatarInitial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    public bool HasImage { get; }
    public bool HasNoImage => !HasImage;
    public ImageSource? ThumbnailSource { get; }

    public GroupSwitcherItemViewModel(string groupId, string name, bool isCurrent, string? imagePath = null)
    {
        GroupId = groupId;
        Name = name;
        IsCurrent = isCurrent;
        ImagePath = imagePath;
        HasImage = !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath);
        ThumbnailSource = HasImage ? ImageSource.FromFile(imagePath!) : null;
    }
}
