using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ActivityPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<CompactEventEntryViewModel> Events { get; } = new();
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

        Events.Clear();
        foreach (var item in GroupPresentationMapper.BuildCompactEvents(overview, workspace.ExpenseIcons))
        {
            Events.Add(item);
        }

        Subtitle = GroupPresentationMapper.FormatCompactPeopleAndEvents(overview);
        OnPropertyChanged(nameof(Subtitle));
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }
}
