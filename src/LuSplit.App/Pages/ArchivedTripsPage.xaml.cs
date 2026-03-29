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

    // Navigate to the group in read-only (archived) mode as a pushed page on top of the stack.
    // This keeps the active-group Home untouched; pressing Back returns to this list.
    private async void OnViewGroupClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string groupId } && !string.IsNullOrWhiteSpace(groupId))
        {
            await Shell.Current.GoToAsync($"{AppRoutes.ArchivedGroupView}?groupId={Uri.EscapeDataString(groupId)}");
        }
    }
}
