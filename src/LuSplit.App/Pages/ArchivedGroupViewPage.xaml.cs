using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class ArchivedGroupViewPage : ContentPage
{
    private readonly ArchivedGroupViewModel _viewModel;
    private readonly AppDataService _dataService;

    public ArchivedGroupViewPage(AppDataService dataService)
    {
        _dataService = dataService;
        _viewModel = new ArchivedGroupViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.ExportRequested += OnExportRequested;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    /// <summary>Must be called before Navigation.PushAsync so LoadAsync has the group ID.</summary>
    public void PrepareForGroup(string groupId)
        => _viewModel.PrepareForGroup(groupId);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnEventSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView) return;

        if (e.CurrentSelection.FirstOrDefault() is not CompactEventEntryViewModel selected
            || !selected.IsExpense
            || string.IsNullOrWhiteSpace(selected.SourceId))
        {
            collectionView.SelectedItem = null;
            return;
        }

        await Shell.Current.GoToAsync($"{AppRoutes.ExpenseDetails}?expenseId={Uri.EscapeDataString(selected.SourceId)}");
        collectionView.SelectedItem = null;
    }

    private async void OnExportRequested(object? sender, string groupId)
    {
        try
        {
            await GroupExportService.RunExportFlowAsync(this, _dataService, groupId);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(null, string.Format(AppResources.Export_Failed, ex.Message), AppResources.Common_Ok);
        }
    }
}
