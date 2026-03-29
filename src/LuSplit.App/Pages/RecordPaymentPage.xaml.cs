using System.Globalization;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public partial class RecordPaymentPage : ContentPage, IQueryAttributable
{
    private readonly RecordPaymentViewModel _viewModel;

public RecordPaymentPage(AppDataService dataService)
    {
        _viewModel = new RecordPaymentViewModel(dataService);
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.PaymentSaved += OnPaymentSaved;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var payerId = query.TryGetValue("payerId", out var p) ? p?.ToString() : null;
        var receiverId = query.TryGetValue("receiverId", out var r) ? r?.ToString() : null;
        var amountMinor = query.TryGetValue("amountMinor", out var amRaw)
            && long.TryParse(amRaw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var am)
            ? am
            : (long?)null;
        var currency = query.TryGetValue("currency", out var cur) ? cur?.ToString() : null;
        var origin = query.TryGetValue("origin", out var orig) ? orig?.ToString() : null;
        _viewModel.SetPrefill(payerId, receiverId, amountMinor, currency, origin);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnPaymentSaved(object? sender, string? origin)
    {
        if (string.Equals(origin, "settlement", StringComparison.OrdinalIgnoreCase))
            await Shell.Current.GoToAsync($"//{AppRoutes.Home}");
        else
            await Shell.Current.GoToAsync("..");
    }
}
