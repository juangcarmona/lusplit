using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Domain.Activity;

namespace LuSplit.App.Features.Activity;

/// <summary>
/// ViewModel for the full-page sync activity feed.
/// Shows a paged list of <see cref="ActivityEntry"/> records for a shared group.
/// </summary>
public sealed partial class ActivityFeedViewModel : ObservableObject
{
    private readonly IActivityFeedDataService _dataService;
    private string _groupId = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    public ObservableCollection<ActivityEntry> Entries { get; } = new();

    public ActivityFeedViewModel(IActivityFeedDataService dataService)
    {
        _dataService = dataService;
    }

    public void SetGroupId(string groupId) => _groupId = groupId;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var entries = await _dataService.GetRecentAsync(_groupId, ct: ct);
            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);
            IsEmpty = Entries.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
