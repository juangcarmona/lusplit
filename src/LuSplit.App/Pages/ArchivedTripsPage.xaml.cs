using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ArchivedGroupsPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<GroupListItemModel> Groups { get; } = new();

    public ArchivedGroupsPage(AppDataService dataService)
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
        var groups = await _dataService.GetArchivedGroupsAsync();

        Groups.Clear();
        foreach (var group in groups)
            Groups.Add(group);
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    // Navigate to group details in read-only (archived) mode.
    // We pass the groupId as a query param so GroupDetailsPage loads that specific group
    // WITHOUT changing the user's currently selected active group.
    private async void OnViewGroupClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await Shell.Current.GoToAsync($"{AppRoutes.GroupTimeline}?groupId={Uri.EscapeDataString(groupId)}");
        }
    }
}
