using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class GroupSwitcherPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<GroupSwitcherItemViewModel> ActiveGroups { get; } = new();
    public ObservableCollection<GroupListItemModel> ArchivedGroups { get; } = new();
    public bool ShowArchived { get; private set; }

    public GroupSwitcherPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var groups = await _dataService.GetGroupsAsync();
        var archived = await _dataService.GetArchivedGroupsAsync();
        ActiveGroups.Clear();
        foreach (var group in groups)
        {
            ActiveGroups.Add(new GroupSwitcherItemViewModel(group.GroupId, group.Name, group.IsCurrent, group.ImagePath));
        }

        ArchivedGroups.Clear();
        foreach (var group in archived)
        {
            ArchivedGroups.Add(group);
        }
    }

    private async void OnSelectGroupClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string groupId } || string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        await _dataService.SelectGroupAsync(groupId);
        await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
    }

    private void OnToggleArchivedClicked(object? sender, EventArgs e)
    {
        ShowArchived = !ShowArchived;
        OnPropertyChanged(nameof(ShowArchived));
    }

    private async void OnNewGroupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.CreateGroup);
    }
}

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
