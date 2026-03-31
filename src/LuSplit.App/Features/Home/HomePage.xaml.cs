using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Persistence;
using LuSplit.App.Services.Settings;
using LuSplit.App.Services.Presentation;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(AppDataService dataService)
    {
        _viewModel = new HomeViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;

        dataService.DataChanged += async (_, _) =>
            await MainThread.InvokeOnMainThreadAsync(_viewModel.LoadAsync);

        _viewModel.TabChanged += (_, _) => ApplyTabButtonStyles();

#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureStartupProfileAsync();
        await _viewModel.LoadAsync();
    }

    private async Task EnsureStartupProfileAsync()
    {
        AppPreferences.InitializePreferredCurrencyIfNeeded();

        if (!string.IsNullOrWhiteSpace(UserProfilePreferences.GetPreferredName())
            || UserProfilePreferences.HasSeenPreferredNamePrompt())
        {
            return;
        }

        var preferredName = await DisplayPromptAsync(
            AppResources.Settings_Title,
            AppResources.Settings_ProfileHint,
            AppResources.Common_Ok,
            AppResources.Common_Cancel,
            AppResources.Settings_MyNamePlaceholder,
            maxLength: 60);

        if (!string.IsNullOrWhiteSpace(preferredName))
            UserProfilePreferences.SetPreferredName(preferredName);

        UserProfilePreferences.MarkPreferredNamePromptSeen();
    }

    private async void OnAddExpenseClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.AddExpense);

    private async void OnCreateGroupClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.CreateGroup);

    private void OnOpenDrawerClicked(object? sender, EventArgs e)
        => Shell.Current.FlyoutIsPresented = true;

    private async void OnOverflowClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.GroupDetails);

    private async void OnSettleUpClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Settlement);

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

    private void ApplyTabButtonStyles()
    {
        var unselected = (Style)MauiApplication.Current!.Resources["SecondaryButton"];
        OverviewTabButton.Style = _viewModel.ShowOverview ? null : unselected;
        ExpensesTabButton.Style = _viewModel.ShowExpenses ? null : unselected;
        BalancesTabButton.Style = _viewModel.ShowBalances ? null : unselected;
    }
}