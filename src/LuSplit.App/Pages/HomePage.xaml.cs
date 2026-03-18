using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<GroupListItemModel> Groups { get; } = new();

    public HomePage(AppDataService dataService)
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
        var groups = await _dataService.GetGroupsAsync();

        Groups.Clear();
        foreach (var group in groups)
            Groups.Add(group);
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }

    private async void OnOpenGroupClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await _dataService.SelectGroupAsync(groupId);
            await Shell.Current.GoToAsync(AppRoutes.GroupTimeline);
        }
    }

    private async void OnEditGroupClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await _dataService.SelectGroupAsync(groupId);
            await Shell.Current.GoToAsync(AppRoutes.GroupDetails);
        }
    }

    private async void OnNewGroupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{AppRoutes.GroupDetails}?mode=create");
    }

    private async void OnViewArchivedClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppRoutes.ArchivedGroups);
    }
}
