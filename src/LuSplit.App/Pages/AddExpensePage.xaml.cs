using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly Dictionary<string, ParticipantModel> _participantById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private string _currency = "USD";
    private bool _isCustomSplit;

    public ObservableCollection<string> PayerNames { get; } = new();
    public ObservableCollection<ParticipantSplitRowViewModel> ParticipantRows { get; } = new();
    public ObservableCollection<ImpactRowViewModel> ImpactRows { get; } = new();

    public string ExpenseTitle { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public string? SelectedPayerName { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public bool CanSave { get; private set; }

    public AddExpensePage(AppDataService dataService)
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
        await LoadParticipantsAsync();
        MainThread.BeginInvokeOnMainThread(() => AmountEntry.Focus());
    }

    private async Task LoadParticipantsAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        var defaults = _dataService.GetEventDraftDefaults();
        _currency = overview.Group.Currency;

        _participants.Clear();
        _participants.AddRange(overview.Participants);
        _participantById.Clear();
        _payerParticipantIdByLabel.Clear();

        PayerNames.Clear();
        ParticipantRows.Clear();
        ImpactRows.Clear();

        foreach (var participant in _participants)
        {
            _participantById[participant.Id] = participant;
            PayerNames.Add(participant.Name);
            _payerParticipantIdByLabel[participant.Name] = participant.Id;
            var isIncluded = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantRows.Add(new ParticipantSplitRowViewModel(participant.Id, participant.Name, isIncluded));
        }

        SelectedPayerName = _participants.FirstOrDefault(participant => string.Equals(participant.Id, defaults.PaidByParticipantId, StringComparison.Ordinal))?.Name
            ?? _participants.FirstOrDefault(participant => string.Equals(participant.Name, UserProfilePreferences.GetPreferredName(), StringComparison.OrdinalIgnoreCase))?.Name
            ?? PayerNames.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPayerName));

        _isCustomSplit = false;
        RecalculateSplits();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!TryParseAmount(AmountText, out var totalMinor))
            {
                StatusText = AppResources.Validation_InvalidAmount;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (string.IsNullOrWhiteSpace(ExpenseTitle))
            {
                StatusText = AppResources.Validation_TitleRequired;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (SelectedPayerName is null || !_payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId))
            {
                StatusText = AppResources.Validation_SelectPayer;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
            if (included.Length < 2)
            {
                StatusText = AppResources.Validation_PickAtLeastOnePerson;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            SplitDefinition splitDefinition;
            if (_isCustomSplit)
            {
                var shares = included.ToDictionary(row => row.Id, row => row.AmountMinor, StringComparer.Ordinal);
                splitDefinition = new SplitDefinition(new SplitComponent[]
                {
                    new FixedSplitComponent(shares)
                });
            }
            else
            {
                splitDefinition = new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(included.Select(row => row.Id).ToArray(), RemainderMode.Equal)
                });
            }

            await _dataService.AddExpenseAsync(ExpenseTitle.Trim(), totalMinor, payerId, ExpenseDate, included.Select(row => row.Id).ToArray(), null, splitDefinition);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e)
    {
        RecalculateSaveState();
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e)
    {
        _isCustomSplit = false;
        RecalculateSplits();
    }

    private void OnPayerChanged(object? sender, EventArgs e)
    {
        RecalculateImpact();
        RecalculateSaveState();
    }

    private void OnParticipantRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string participantId)
        {
            return;
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        row.IsIncluded = !row.IsIncluded;
        if (!row.IsIncluded)
        {
            row.AmountMinor = 0;
        }

        _isCustomSplit = false;
        RecalculateSplits();
    }

    private void OnDecreaseParticipantAmountClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantId })
        {
            return;
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        if (row is null || !row.IsIncluded)
        {
            return;
        }

        _isCustomSplit = true;
        row.AmountMinor = Math.Max(0, row.AmountMinor - 100);
        NormalizeCustomSplitToTotal();
        RecalculateSplits();
    }

    private void OnIncreaseParticipantAmountClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantId })
        {
            return;
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        if (row is null || !row.IsIncluded)
        {
            return;
        }

        _isCustomSplit = true;
        row.AmountMinor += 100;
        NormalizeCustomSplitToTotal();
        RecalculateSplits();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = Shell.Current.GoToAsync("..");
        return true;
    }

    private void RecalculateSplits()
    {
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        foreach (var row in ParticipantRows.Where(candidate => !candidate.IsIncluded))
        {
            row.AmountMinor = 0;
            row.AmountText = FormatMinor(0, _currency);
            row.Notify();
        }

        if (!TryParseAmount(AmountText, out var totalMinor) || included.Length == 0)
        {
            foreach (var row in included)
            {
                row.AmountMinor = 0;
                row.AmountText = FormatMinor(0, _currency);
                row.Notify();
            }

            RecalculateImpact();
            RecalculateSaveState();
            return;
        }

        if (_isCustomSplit)
        {
            NormalizeCustomSplitToTotal();
        }
        else
        {
            var baseShare = totalMinor / included.Length;
            var remainder = (int)(totalMinor % included.Length);
            for (var index = 0; index < included.Length; index++)
            {
                included[index].AmountMinor = baseShare + (index < remainder ? 1 : 0);
                included[index].AmountText = FormatMinor(included[index].AmountMinor, _currency);
                included[index].Notify();
            }
        }

        foreach (var row in included)
        {
            row.AmountText = FormatMinor(row.AmountMinor, _currency);
            row.Notify();
        }

        RecalculateImpact();
        RecalculateSaveState();
    }

    private void NormalizeCustomSplitToTotal()
    {
        if (!TryParseAmount(AmountText, out var totalMinor))
        {
            return;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length == 0)
        {
            return;
        }

        var sum = included.Sum(row => row.AmountMinor);
        if (sum == totalMinor)
        {
            return;
        }

        if (sum <= 0)
        {
            included[0].AmountMinor = totalMinor;
            for (var index = 1; index < included.Length; index++)
            {
                included[index].AmountMinor = 0;
            }

            return;
        }

        var delta = totalMinor - sum;
        if (delta > 0)
        {
            included[^1].AmountMinor += delta;
            return;
        }

        var remainingToReduce = -delta;
        for (var index = included.Length - 1; index >= 0 && remainingToReduce > 0; index--)
        {
            var reducible = Math.Min(included[index].AmountMinor, remainingToReduce);
            included[index].AmountMinor -= reducible;
            remainingToReduce -= reducible;
        }

        if (remainingToReduce > 0)
        {
            included[0].AmountMinor += remainingToReduce;
        }
    }

    private void RecalculateImpact()
    {
        ImpactRows.Clear();

        if (!TryParseAmount(AmountText, out var totalMinor)
            || SelectedPayerName is null
            || !_payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId))
        {
            return;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length == 0)
        {
            return;
        }

        foreach (var row in included.Where(row => !string.Equals(row.Id, payerId, StringComparison.Ordinal)))
        {
            if (row.AmountMinor <= 0)
            {
                continue;
            }

            ImpactRows.Add(new ImpactRowViewModel(
                $"{row.Name} → {SelectedPayerName} {FormatMinor(row.AmountMinor, _currency)}"));
            if (ImpactRows.Count >= 4)
            {
                break;
            }
        }
    }

    private void RecalculateSaveState()
    {
        var hasAmount = TryParseAmount(AmountText, out _);
        var hasTitle = !string.IsNullOrWhiteSpace(ExpenseTitle);
        var hasPayer = SelectedPayerName is not null && _payerParticipantIdByLabel.ContainsKey(SelectedPayerName);
        var includedCount = ParticipantRows.Count(row => row.IsIncluded);

        CanSave = hasAmount && hasTitle && hasPayer && includedCount >= 2;
        OnPropertyChanged(nameof(CanSave));
    }

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

    private static string FormatMinor(long minor, string currency)
    {
        var amount = minor / 100m;
        var symbol = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => string.Empty
        };

        return string.IsNullOrEmpty(symbol)
            ? string.Create(CultureInfo.CurrentCulture, $"{amount:0.00} {currency.ToUpperInvariant()}")
            : string.Create(CultureInfo.CurrentCulture, $"{symbol}{amount:0.00}");
    }

    public sealed class ParticipantSplitRowViewModel : BindableObject
    {
        private bool _isIncluded;
        private long _amountMinor;
        private string _amountText = "0.00";

        public string Id { get; }
        public string Name { get; }

        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                if (_isIncluded == value)
                {
                    return;
                }

                _isIncluded = value;
                OnPropertyChanged();
            }
        }

        public long AmountMinor
        {
            get => _amountMinor;
            set
            {
                if (_amountMinor == value)
                {
                    return;
                }

                _amountMinor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AmountText));
            }
        }

        public string AmountText
        {
            get => _amountText;
            set
            {
                if (string.Equals(_amountText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _amountText = value;
                OnPropertyChanged();
            }
        }

        public ParticipantSplitRowViewModel(string id, string name, bool isIncluded)
        {
            Id = id;
            Name = name;
            _isIncluded = isIncluded;
            _amountMinor = 0;
        }

        public void Notify()
        {
            OnPropertyChanged(nameof(IsIncluded));
            OnPropertyChanged(nameof(AmountText));
        }
    }

    public sealed record ImpactRowViewModel(string Text);
}
