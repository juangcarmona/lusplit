using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ActivityPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<ActivityCompactDayGroupViewModel> ActivityGroups { get; } = new();
    public string Subtitle { get; private set; } = string.Empty;

    public ActivityPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;

        _dataService.DataChanged += OnDataChanged;
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
        var workspace = await _dataService.GetGroupWorkspaceAsync();
        var overview = workspace.Overview;

        var events = GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons);
        var grouped = events
            .GroupBy(item => GroupPresentationMapper.DescribeDay(item.SortDate))
            .ToArray();

        ActivityGroups.Clear();
        foreach (var group in grouped)
        {
            ActivityGroups.Add(new ActivityCompactDayGroupViewModel(group.Key, group.ToArray()));
        }

        Subtitle = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        OnPropertyChanged(nameof(Subtitle));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }
}

public sealed class ActivityCompactDayGroupViewModel : ObservableCollection<CompactEventEntryViewModel>
{
    public string Title { get; }

    public ActivityCompactDayGroupViewModel(string title, IEnumerable<CompactEventEntryViewModel> items)
        : base(items)
    {
        Title = title;
    }
}
