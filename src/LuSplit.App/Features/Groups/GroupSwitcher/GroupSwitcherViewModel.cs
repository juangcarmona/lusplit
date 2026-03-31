using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.GroupSwitcher;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Pages;

public sealed partial class GroupSwitcherViewModel : ObservableObject
{
    private readonly IGroupSwitcherDataService _dataService;

    [ObservableProperty] private bool _showArchived;

    public ObservableCollection<GroupSwitcherItemViewModel> ActiveGroups { get; } = new();
    public ObservableCollection<GroupListItemModel> ArchivedGroups { get; } = new();

    public event EventHandler<string>? GroupSelected;
    public event EventHandler? NewGroupRequested;

    public GroupSwitcherViewModel(IGroupSwitcherDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadAsync()
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

    [RelayCommand]
    private async Task SelectGroup(string groupId)
    {
        await _dataService.SelectGroupAsync(groupId);
        GroupSelected?.Invoke(this, groupId);
    }

    [RelayCommand]
    private void ToggleArchived() => ShowArchived = !ShowArchived;

    [RelayCommand]
    private void NavigateToNewGroup() => NewGroupRequested?.Invoke(this, EventArgs.Empty);
}
