using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

using MauiApplication = Microsoft.Maui.Controls.Application;


namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private enum SplitMode
    {
        Equal,
        Exact,
        Percent
    }

    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private SplitMode _splitMode = SplitMode.Equal;
    private string _currency = "USD";

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
        PayerNames.Clear();
        ParticipantRows.Clear();
        ImpactRows.Clear();
        _payerParticipantIdByLabel.Clear();

        foreach (var participant in _participants)
        {
            PayerNames.Add(participant.Name);
            _payerParticipantIdByLabel[participant.Name] = participant.Id;
            var isIncluded = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantRows.Add(new ParticipantSplitRowViewModel(participant.Id, participant.Name, isIncluded));
        }

        SelectedPayerName = _participants.FirstOrDefault(participant => string.Equals(participant.Id, defaults.PaidByParticipantId, StringComparison.Ordinal))?.Name
            ?? _participants.FirstOrDefault(participant => string.Equals(participant.Name, UserProfilePreferences.GetPreferredName(), StringComparison.OrdinalIgnoreCase))?.Name
            ?? PayerNames.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPayerName));

        RecalculateAll();
        ApplySplitModeVisualState();
    }

    private void OnEqualModeClicked(object? sender, EventArgs e)
    {
        _splitMode = SplitMode.Equal;
        RecalculateAll();
        ApplySplitModeVisualState();
    }

    private void OnExactModeClicked(object? sender, EventArgs e)
    {
        _splitMode = SplitMode.Exact;
        RecalculateAll();
        ApplySplitModeVisualState();
    }

    private void OnPercentModeClicked(object? sender, EventArgs e)
    {
        _splitMode = SplitMode.Percent;
        RecalculateAll();
        ApplySplitModeVisualState();
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

            SplitDefinition splitDefinition = _splitMode switch
            {
                SplitMode.Equal => new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(included.Select(row => row.Id).ToArray(), RemainderMode.Equal)
                }),
                SplitMode.Percent => new SplitDefinition(new SplitComponent[]
                {
                    new RemainderSplitComponent(
                        included.Select(row => row.Id).ToArray(),
                        RemainderMode.Percent,
                        null,
                        included.ToDictionary(row => row.Id, row => row.PercentValue, StringComparer.Ordinal))
                }),
                _ => new SplitDefinition(new SplitComponent[]
                {
                    new FixedSplitComponent(included.ToDictionary(row => row.Id, row => row.AmountMinor, StringComparer.Ordinal))
                })
            };

            var title = string.IsNullOrWhiteSpace(ExpenseTitle) ? AppResources.AddEvent_QuickCustom : ExpenseTitle.Trim();

            await _dataService.AddExpenseAsync(
                title,
                totalMinor,
                payerId,
                ExpenseDate,
                included.Select(row => row.Id).ToArray(),
                null,
                splitDefinition);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e)
    {
        RecalculateAll();
    }

    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e)
    {
        RecalculateSaveState();
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
            row.PercentValue = 0;
        }

        RecalculateAll();
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

        switch (_splitMode)
        {
            case SplitMode.Percent:
                row.PercentValue = Math.Max(0, row.PercentValue - 1);
                break;
            default:
                row.AmountMinor = Math.Max(0, row.AmountMinor - 100);
                break;
        }

        RecalculateAll();
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

        switch (_splitMode)
        {
            case SplitMode.Percent:
                row.PercentValue += 1;
                break;
            default:
                row.AmountMinor += 100;
                break;
        }

        RecalculateAll();
    }

    private void OnParticipantAmountEdited(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry { BindingContext: ParticipantSplitRowViewModel row } || !row.IsIncluded)
        {
            return;
        }

        switch (_splitMode)
        {
            case SplitMode.Percent:
                var percentText = NormalizeEditableInput(e.NewTextValue);
                if (int.TryParse(percentText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var pct)
                    || int.TryParse(percentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out pct))
                {
                    row.PercentValue = Math.Max(0, pct);
                }
                break;
            default:
                var amountText = NormalizeEditableInput(e.NewTextValue);
                if (TryParseAmount(amountText, out var amount))
                {
                    row.AmountMinor = amount;
                }
                break;
        }

        RecalculateAll();
    }

    private void OnParticipantAmountEditCompleted(object? sender, EventArgs e)
    {
        RecalculateAll();
    }

    private void RecalculateAll()
    {
        StatusText = string.Empty;
        OnPropertyChanged(nameof(StatusText));

        switch (_splitMode)
        {
            case SplitMode.Equal:
                RecalculateEqual();
                break;
            case SplitMode.Exact:
                RecalculateExact();
                break;
            case SplitMode.Percent:
                RecalculatePercent();
                break;
        }

        RecalculateImpact();
        RecalculateSaveState();
        RefreshEditableValues();
    }

    private void RecalculateEqual()
    {
        if (!TryParseAmount(AmountText, out var totalMinor))
        {
            SetAllIncludedToZero();
            return;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length == 0)
        {
            return;
        }

        var baseShare = totalMinor / included.Length;
        var remainder = (int)(totalMinor % included.Length);

        for (var index = 0; index < included.Length; index++)
        {
            included[index].AmountMinor = baseShare + (index < remainder ? 1 : 0);
        }

        foreach (var row in ParticipantRows.Where(row => !row.IsIncluded))
        {
            row.AmountMinor = 0;
            row.PercentValue = 0;
        }
    }

    private void RecalculateExact()
    {
        if (!TryParseAmount(AmountText, out var totalMinor))
        {
            SetAllIncludedToZero();
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

    private void RecalculatePercent()
    {
        if (!TryParseAmount(AmountText, out var totalMinor))
        {
            SetAllIncludedToZero();
            return;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length == 0)
        {
            return;
        }

        var totalPercent = included.Sum(row => row.PercentValue);
        if (totalPercent <= 0)
        {
            var equal = 100 / included.Length;
            var remainder = 100 % included.Length;
            for (var index = 0; index < included.Length; index++)
            {
                included[index].PercentValue = equal + (index < remainder ? 1 : 0);
            }
        }

        var normalizedTotalPercent = included.Sum(row => row.PercentValue);
        if (normalizedTotalPercent != 100)
        {
            var delta = 100 - normalizedTotalPercent;
            if (delta > 0)
            {
                included[^1].PercentValue += delta;
            }
            else
            {
                var remainingToReduce = -delta;
                for (var index = included.Length - 1; index >= 0 && remainingToReduce > 0; index--)
                {
                    var reducible = Math.Min(included[index].PercentValue, remainingToReduce);
                    included[index].PercentValue -= reducible;
                    remainingToReduce -= reducible;
                }
            }
        }

        long assigned = 0;
        for (var index = 0; index < included.Length; index++)
        {
            if (index == included.Length - 1)
            {
                included[index].AmountMinor = totalMinor - assigned;
                continue;
            }

            var share = (long)Math.Round(totalMinor * (included[index].PercentValue / 100m), MidpointRounding.AwayFromZero);
            included[index].AmountMinor = Math.Max(0, share);
            assigned += included[index].AmountMinor;
        }
    }

    private void SetAllIncludedToZero()
    {
        foreach (var row in ParticipantRows)
        {
            row.AmountMinor = 0;
            if (!row.IsIncluded)
            {
                row.PercentValue = 0;
            }
        }
    }

    private void RecalculateImpact()
    {
        ImpactRows.Clear();

        if (SelectedPayerName is null || !_payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId))
        {
            return;
        }

        foreach (var row in ParticipantRows.Where(row => row.IsIncluded && !string.Equals(row.Id, payerId, StringComparison.Ordinal)))
        {
            if (row.AmountMinor <= 0)
            {
                continue;
            }

            ImpactRows.Add(new ImpactRowViewModel($"{row.Name} → {SelectedPayerName} {FormatMinor(row.AmountMinor, _currency)}"));
            if (ImpactRows.Count >= 4)
            {
                break;
            }
        }
    }

    private void RecalculateSaveState()
    {
        var hasAmount = TryParseAmount(AmountText, out _);
        var hasPayer = SelectedPayerName is not null && _payerParticipantIdByLabel.ContainsKey(SelectedPayerName);
        var includedCount = ParticipantRows.Count(row => row.IsIncluded);
        CanSave = hasAmount && hasPayer && includedCount >= 2;
        OnPropertyChanged(nameof(CanSave));
    }

    private void ApplySplitModeVisualState()
    {
        var unselected = (Style)MauiApplication.Current!.Resources["SecondaryButton"];
        EqualModeButton.Style = _splitMode == SplitMode.Equal ? null : unselected;
        ExactModeButton.Style = _splitMode == SplitMode.Exact ? null : unselected;
        PercentModeButton.Style = _splitMode == SplitMode.Percent ? null : unselected;
    }

    private void RefreshEditableValues()
    {
        foreach (var row in ParticipantRows)
        {
            row.UpdateEditableValue(_splitMode switch
            {
                SplitMode.Percent => $"{row.PercentValue}%",
                _ => FormatMinor(row.AmountMinor, _currency)
            });
        }
    }

    private static string NormalizeEditableInput(string? text)
        => (text ?? string.Empty)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Trim();

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
            ? $"{amount:0.00} {currency.ToUpperInvariant()}"
            : $"{symbol}{amount:0.00}";
    }

    public sealed class ParticipantSplitRowViewModel : BindableObject
    {
        private bool _isIncluded;
        private long _amountMinor;
        private int _percentValue;
        private string _editableValue = "0";

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
                OnPropertyChanged(nameof(IsIncludedMark));
            }
        }

        public string IsIncludedMark => _isIncluded ? "✓" : " ";

        public long AmountMinor
        {
            get => _amountMinor;
            set
            {
                if (_amountMinor == value)
                {
                    return;
                }

                _amountMinor = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        public int PercentValue
        {
            get => _percentValue;
            set
            {
                if (_percentValue == value)
                {
                    return;
                }

                _percentValue = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        public string EditableValue
        {
            get => _editableValue;
            set
            {
                if (string.Equals(_editableValue, value, StringComparison.Ordinal))
                {
                    return;
                }

                _editableValue = value;
                OnPropertyChanged();
            }
        }

        public ParticipantSplitRowViewModel(string id, string name, bool isIncluded)
        {
            Id = id;
            Name = name;
            _isIncluded = isIncluded;
        }

        public void UpdateEditableValue(string value)
        {
            EditableValue = value;
        }
    }

    public sealed record ImpactRowViewModel(string Text);
}
