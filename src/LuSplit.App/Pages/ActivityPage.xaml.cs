using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ActivityPage : ContentPage
{
    private readonly AppDataService _dataService;

    public ObservableCollection<ActivityDayGroupViewModel> ActivityGroups { get; } = new();

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
        var overview = await _dataService.GetOverviewAsync();
        var groups = TripPresentationMapper.BuildActivity(overview);

        ActivityGroups.Clear();
        foreach (var group in groups)
        {
            ActivityGroups.Add(group);
        }
    }

    private async void OnDataChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(LoadAsync);
    }
}
