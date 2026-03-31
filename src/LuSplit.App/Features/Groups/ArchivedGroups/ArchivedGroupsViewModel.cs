using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Groups.ArchivedGroups;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.ArchivedGroups;

public sealed partial class ArchivedGroupsViewModel : ObservableObject
{
    private readonly IArchivedGroupsDataService _dataService;

    public ObservableCollection<GroupListItemModel> Groups { get; } = new();

    public event EventHandler<string>? ViewGroupRequested;

    public ArchivedGroupsViewModel(IArchivedGroupsDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadAsync()
    {
        var groups = await _dataService.GetArchivedGroupsAsync();

        Groups.Clear();
        foreach (var group in groups)
            Groups.Add(group);
    }

    public Task HandleDataChangedAsync() => LoadAsync();

    [RelayCommand]
    private void ViewGroup(string groupId)
    {
        if (!string.IsNullOrWhiteSpace(groupId))
            ViewGroupRequested?.Invoke(this, groupId);
    }
}
