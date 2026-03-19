using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;

namespace LuSplit.App.Pages;

public partial class RecordPaymentPage : LoadOnAppearingPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private string? _prefillPayerId;
    private string? _prefillReceiverId;
    private long? _prefillAmountMinor;

    public ObservableCollection<string> PersonNames { get; } = new();

    public string? SelectedFromName { get; set; }

    public string? SelectedToName { get; set; }

    public string AmountText { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; } = DateTime.Today;

    public string StatusText { get; set; } = string.Empty;
    public bool IsQuickMode { get; private set; }
    public bool IsManualMode => !IsQuickMode;
    public string QuickSummaryText { get; private set; } = string.Empty;

    public RecordPaymentPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
#if ANDROID
        BottomBanner.AdsId = AdMobConfig.BannerId;
#endif
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _prefillPayerId = query.TryGetValue("payerId", out var payerId) ? payerId?.ToString() : null;
        _prefillReceiverId = query.TryGetValue("receiverId", out var receiverId) ? receiverId?.ToString() : null;
        _prefillAmountMinor = query.TryGetValue("amountMinor", out var amountMinorRaw)
            && long.TryParse(amountMinorRaw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amountMinor)
            ? amountMinor
            : null;
    }

    protected override async Task LoadAsync()
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
        SelectedFromName = ResolveParticipantName(_prefillPayerId ?? suggestion?.FromParticipantId) ?? PersonNames.FirstOrDefault();
        SelectedToName = ResolveParticipantName(_prefillReceiverId ?? suggestion?.ToParticipantId)
            ?? PersonNames.Skip(SelectedFromName is null ? 0 : 1).FirstOrDefault()
            ?? PersonNames.FirstOrDefault();
        AmountText = _prefillAmountMinor is long prefillMinor
            ? (prefillMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture)
            : AmountText;

        IsQuickMode = !string.IsNullOrWhiteSpace(_prefillPayerId)
            && !string.IsNullOrWhiteSpace(_prefillReceiverId)
            && _prefillAmountMinor is > 0;
        QuickSummaryText = IsQuickMode
            ? string.Create(CultureInfo.CurrentCulture, $"{SelectedFromName} → {SelectedToName}")
            : string.Empty;

        OnPropertyChanged(nameof(SelectedFromName));
        OnPropertyChanged(nameof(SelectedToName));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(IsQuickMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(QuickSummaryText));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var from = _participants.FirstOrDefault(participant => participant.Name == SelectedFromName);
            var to = _participants.FirstOrDefault(participant => participant.Name == SelectedToName);

            if (from is null || to is null)
            {
                StatusText = AppResources.Validation_ChooseBothPeople;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (string.Equals(from.Id, to.Id, StringComparison.Ordinal))
            {
                StatusText = AppResources.Validation_DifferentPeople;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!TryParseAmount(AmountText, out var amountMinor))
            {
                StatusText = AppResources.Validation_InvalidAmount;
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
