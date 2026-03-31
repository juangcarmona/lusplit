using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LuSplit.App.Services.Presentation;

namespace LuSplit.App.Features.Activity.Activity;

public sealed partial class ActivityViewModel : ObservableObject
{
    private readonly IActivityDataService _dataService;

    [ObservableProperty] private string _subtitle = string.Empty;

    public ObservableCollection<ActivityCompactDayGroupViewModel> ActivityGroups { get; } = new();

    public ActivityViewModel(IActivityDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadAsync()
    {
        var workspace = await _dataService.GetGroupWorkspaceAsync();
        var overview = workspace.Overview;

        var events = GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons);
        var grouped = events
            .GroupBy(item => GroupPresentationMapper.DescribeDay(item.SortDate))
            .ToArray();

        ActivityGroups.Clear();
        foreach (var group in grouped)
            ActivityGroups.Add(new ActivityCompactDayGroupViewModel(group.Key, group.ToArray()));

        Subtitle = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
    }

    public Task HandleDataChangedAsync() => LoadAsync();
}
