using LuSplit.App.Features.Groups.ArchivedGroupView;
using LuSplit.App.Services.Persistence;

namespace LuSplit.App.Features.Groups.ArchivedGroups;

public partial class ArchivedGroupsPage : ContentPage
{
    private readonly ArchivedGroupsViewModel _viewModel;

    public ArchivedGroupsPage(AppDataService dataService)
    {
        _viewModel = new ArchivedGroupsViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;

        dataService.DataChanged += async (_, _) =>
            await MainThread.InvokeOnMainThreadAsync(_viewModel.HandleDataChangedAsync);

        // Push a dedicated ArchivedGroupViewPage onto the navigation stack.
        // Using a separate page type (not HomePage) is essential — MAUI's Shell URI
        // resolver sees the same type in two contexts when HomePage is pushed, producing
        // "Ambiguous routes" and breaking the toolbar back button.
        _viewModel.ViewGroupRequested += async (_, groupId) =>
        {
            var page = App.Services!.GetRequiredService<ArchivedGroupViewPage>();
            page.PrepareForGroup(groupId);
            await Navigation.PushAsync(page);
        };

#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
