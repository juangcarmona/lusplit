using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class RecordPaymentPage : ContentPage
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();

    public ObservableCollection<string> PersonNames { get; } = new();

    public string? SelectedFromName { get; set; }

    public string? SelectedToName { get; set; }

    public string AmountText { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; } = DateTime.Today;

    public string StatusText { get; set; } = string.Empty;

    public RecordPaymentPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
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
        _participants.Clear();
        _participants.AddRange(overview.Participants);

        PersonNames.Clear();
        foreach (var participant in _participants)
        {
            PersonNames.Add(participant.Name);
        }

        var suggestion = overview.SettlementByParticipant.Transfers.FirstOrDefault();
        SelectedFromName = ResolveParticipantName(suggestion?.FromParticipantId) ?? PersonNames.FirstOrDefault();
        SelectedToName = ResolveParticipantName(suggestion?.ToParticipantId)
            ?? PersonNames.Skip(SelectedFromName is null ? 0 : 1).FirstOrDefault()
            ?? PersonNames.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedFromName));
        OnPropertyChanged(nameof(SelectedToName));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var from = _participants.FirstOrDefault(participant => participant.Name == SelectedFromName);
            var to = _participants.FirstOrDefault(participant => participant.Name == SelectedToName);

            if (from is null || to is null)
            {
                StatusText = "Choose both people.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (string.Equals(from.Id, to.Id, StringComparison.Ordinal))
            {
                StatusText = "Payment needs two different people.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!TryParseAmount(AmountText, out var amountMinor))
            {
                StatusText = "Enter a valid amount.";
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            await _dataService.AddPaymentAsync(from.Id, to.Id, amountMinor, PaymentDate);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private string? ResolveParticipantName(string? participantId)
        => _participants.FirstOrDefault(participant => string.Equals(participant.Id, participantId, StringComparison.Ordinal))?.Name;

    private static bool TryParseAmount(string text, out long amountMinor)
    {
        var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
        if (decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var invariant)
            || decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out invariant))
        {
            amountMinor = (long)Math.Round(invariant * 100m, MidpointRounding.AwayFromZero);
            return amountMinor > 0;
        }

        amountMinor = 0;
        return false;
    }
}
