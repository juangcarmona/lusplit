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

    // Push a dedicated ArchivedGroupViewPage onto the navigation stack.
    // Using a separate page type (not HomePage) is essential — MAUI's Shell URI
    // resolver sees the same type in two contexts when HomePage is pushed, producing
    // "Ambiguous routes" and breaking the toolbar back button.
    private async void OnViewGroupClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            var page = App.Services!.GetRequiredService<ArchivedGroupViewPage>();
            page.PrepareForGroup(groupId);
            await Navigation.PushAsync(page);
        }
    }
}
