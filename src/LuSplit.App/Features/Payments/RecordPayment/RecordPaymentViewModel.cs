using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Payments.RecordPayment;
using LuSplit.App.Resources.Localization;
using LuSplit.Application.Groups.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace LuSplit.App.Features.Payments.RecordPayment;

public sealed partial class RecordPaymentViewModel : ObservableObject
{
    private readonly IRecordPaymentDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private string? _prefillPayerId;
    private string? _prefillReceiverId;
    private long? _prefillAmountMinor;
    private string? _prefillCurrency;
    private string? _origin;

    public ObservableCollection<string> PersonNames { get; } = new();

    [ObservableProperty]
    private string? _selectedFromName;

    [ObservableProperty]
    private string? _selectedToName;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    private DateTime _paymentDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _paymentTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isQuickMode;

    [ObservableProperty]
    private string _quickSummaryText = string.Empty;

    /// <summary>Raised after a payment is saved. Argument is the origin query parameter value.</summary>
    public event EventHandler<string?>? PaymentSaved;

    public RecordPaymentViewModel(IRecordPaymentDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>Stores prefill values from query attributes before <see cref="LoadAsync"/> is called.</summary>
    public void SetPrefill(string? payerId, string? receiverId, long? amountMinor, string? currency, string? origin)
    {
        _prefillPayerId = payerId;
        _prefillReceiverId = receiverId;
        _prefillAmountMinor = amountMinor;
        _prefillCurrency = currency;
        _origin = origin;
    }

    public async Task LoadAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        _participants.Clear();
        _participants.AddRange(overview.Participants);

        PersonNames.Clear();
        foreach (var participant in _participants)
            PersonNames.Add(participant.Name);

        var suggestion = overview.SettlementByParticipant.Transfers.FirstOrDefault();
        SelectedFromName = ResolveParticipantName(_prefillPayerId ?? suggestion?.FromParticipantId)
            ?? PersonNames.FirstOrDefault();
        SelectedToName = ResolveParticipantName(_prefillReceiverId ?? suggestion?.ToParticipantId)
            ?? PersonNames.Skip(SelectedFromName is null ? 0 : 1).FirstOrDefault()
            ?? PersonNames.FirstOrDefault();

        if (_prefillAmountMinor is long prefillMinor)
            AmountText = (prefillMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

        IsQuickMode = !string.IsNullOrWhiteSpace(_prefillPayerId)
            && !string.IsNullOrWhiteSpace(_prefillReceiverId)
            && _prefillAmountMinor is > 0;
        QuickSummaryText = IsQuickMode
            ? string.Create(CultureInfo.CurrentCulture,
                $"{SelectedFromName} → {SelectedToName}{(string.IsNullOrWhiteSpace(_prefillCurrency) ? string.Empty : $" ({_prefillCurrency})")}")
            : string.Empty;
    }

    [RelayCommand]
    private async Task Save()
    {
        try
        {
            var from = _participants.FirstOrDefault(p => p.Name == SelectedFromName);
            var to = _participants.FirstOrDefault(p => p.Name == SelectedToName);

            if (from is null || to is null)
            {
                StatusText = AppResources.Validation_ChooseBothPeople;
                return;
            }

            if (string.Equals(from.Id, to.Id, StringComparison.Ordinal))
            {
                StatusText = AppResources.Validation_DifferentPeople;
                return;
            }

            if (!ExpenseAmountParser.TryParseCommittedAmount(AmountText, out var amountMinor) || amountMinor <= 0)
            {
                StatusText = AppResources.Validation_InvalidAmount;
                return;
            }

            var paymentDateTime = PaymentDate.Date.Add(PaymentTime);
            await _dataService.AddPaymentAsync(from.Id, to.Id, amountMinor, paymentDateTime);
            PaymentSaved?.Invoke(this, _origin);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    private string? ResolveParticipantName(string? participantId)
        => _participants.FirstOrDefault(p => string.Equals(p.Id, participantId, StringComparison.Ordinal))?.Name;
}
