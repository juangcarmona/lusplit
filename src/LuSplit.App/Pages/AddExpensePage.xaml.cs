using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Pages;

public partial class AddExpensePage : ContentPage
{
    private enum RowSplitMode
    {
        Auto,
        Exact,
        Percent
    }

    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private string _currency = "USD";
    private string? _attachmentLabel;

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
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!CanSave || !TryParseAmount(AmountText, out var totalMinor))
            {
                return;
            }

            if (SelectedPayerName is null || !_payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId))
            {
                return;
            }

            var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
            var exactRows = included.Where(row => row.Mode == RowSplitMode.Exact).ToArray();
            var percentRows = included.Where(row => row.Mode == RowSplitMode.Percent).ToArray();
            var autoRows = included.Where(row => row.Mode == RowSplitMode.Auto).ToArray();

            var components = new List<SplitComponent>();
            if (exactRows.Length > 0)
            {
                components.Add(new FixedSplitComponent(exactRows.ToDictionary(row => row.Id, row => row.AmountMinor, StringComparer.Ordinal)));
            }

            if (percentRows.Length > 0)
            {
                components.Add(new RemainderSplitComponent(
                    percentRows.Select(row => row.Id).ToArray(),
                    RemainderMode.Percent,
                    null,
                    percentRows.ToDictionary(row => row.Id, row => row.PercentValue, StringComparer.Ordinal)));
            }

            if (autoRows.Length > 0)
            {
                components.Add(new RemainderSplitComponent(autoRows.Select(row => row.Id).ToArray(), RemainderMode.Equal));
            }

            var splitDefinition = new SplitDefinition(components.ToArray());
            var title = string.IsNullOrWhiteSpace(ExpenseTitle) ? AppResources.AddEvent_QuickCustom : ExpenseTitle.Trim();

            await _dataService.AddExpenseAsync(
                title,
                totalMinor,
                payerId,
                ExpenseDate,
                included.Select(row => row.Id).ToArray(),
                _attachmentLabel,
                splitDefinition);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e) => RecalculateAll();
    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e) => RecalculateSaveState();

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
            row.Mode = RowSplitMode.Auto;
            row.AmountMinor = 0;
            row.PercentValue = 0;
        }

        RecalculateAll();
    }

    private void OnCycleModeClicked(object? sender, EventArgs e)
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

        row.Mode = row.Mode switch
        {
            RowSplitMode.Auto => RowSplitMode.Exact,
            RowSplitMode.Exact => RowSplitMode.Percent,
            _ => RowSplitMode.Auto
        };

        RecalculateAll();
    }

    private void OnDecreaseParticipantAmountClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantId })
        {
            return;
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        if (row is null || !row.IsValueEditable)
        {
            return;
        }

        if (row.Mode == RowSplitMode.Percent)
        {
            row.PercentValue = Math.Max(0, row.PercentValue - 1);
        }
        else
        {
            row.AmountMinor = Math.Max(0, row.AmountMinor - 100);
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
        if (row is null || !row.IsValueEditable)
        {
            return;
        }

        if (row.Mode == RowSplitMode.Percent)
        {
            row.PercentValue += 1;
        }
        else
        {
            row.AmountMinor += 100;
        }

        RecalculateAll();
    }

    private void OnParticipantAmountEdited(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry { BindingContext: ParticipantSplitRowViewModel row } || !row.IsValueEditable)
        {
            return;
        }

        try
        {
            var normalized = NormalizeEditableInput(e.NewTextValue);
            if (row.Mode == RowSplitMode.Percent)
            {
                if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out var pct)
                    || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out pct))
                {
                    row.PercentValue = Math.Max(0, pct);
                }
            }
            else if (TryParseAmount(normalized, out var amount))
            {
                row.AmountMinor = amount;
            }
        }
        catch
        {
            // Avoid edit-time crashes from malformed user input.
        }

        RecalculateAll();
    }

    private void OnParticipantAmountEditCompleted(object? sender, EventArgs e) => RecalculateAll();

    private async void OnAttachMediaClicked(object? sender, EventArgs e)
    {
        _attachmentLabel = "attachment";
        StatusText = "Attachment will be linked after save.";
        OnPropertyChanged(nameof(StatusText));
        await Task.CompletedTask;
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        _attachmentLabel = "photo";
        StatusText = "Photo will be linked after save.";
        OnPropertyChanged(nameof(StatusText));
        await Task.CompletedTask;
    }

    private void RecalculateAll()
    {
        var validationMessage = ValidateAndComputeRows();
        StatusText = validationMessage;
        OnPropertyChanged(nameof(StatusText));
        RecalculateImpact();
        RecalculateSaveState();
        RefreshRowsDisplay();
    }

    private string ValidateAndComputeRows()
    {
        if (!TryParseAmount(AmountText, out var totalMinor))
        {
            ResetAllShares();
            return AppResources.Validation_InvalidAmount;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length < 2)
        {
            ResetAllShares();
            return AppResources.Validation_PickAtLeastOnePerson;
        }

        var exactRows = included.Where(row => row.Mode == RowSplitMode.Exact).ToArray();
        var percentRows = included.Where(row => row.Mode == RowSplitMode.Percent).ToArray();
        var autoRows = included.Where(row => row.Mode == RowSplitMode.Auto).ToArray();

        var exactTotal = exactRows.Sum(row => row.AmountMinor);
        var totalPercent = percentRows.Sum(row => row.PercentValue);
        if (totalPercent > 100)
        {
            ResetAutoRows(autoRows);
            return AppResources.Validation_InvalidAmount;
        }

        long percentMinor = 0;
        foreach (var row in percentRows)
        {
            percentMinor += (long)Math.Round(totalMinor * (row.PercentValue / 100m), MidpointRounding.AwayFromZero);
        }

        var remaining = totalMinor - exactTotal - percentMinor;
        if (remaining < 0)
        {
            ResetAutoRows(autoRows);
            return AppResources.Validation_InvalidAmount;
        }

        if (autoRows.Length == 0)
        {
            if (remaining != 0)
            {
                return AppResources.Validation_InvalidAmount;
            }
        }
        else
        {
            var baseShare = remaining / autoRows.Length;
            var remainder = (int)(remaining % autoRows.Length);
            for (var index = 0; index < autoRows.Length; index++)
            {
                autoRows[index].AmountMinor = baseShare + (index < remainder ? 1 : 0);
            }
        }

        foreach (var row in ParticipantRows.Where(row => !row.IsIncluded))
        {
            row.AmountMinor = 0;
            row.PercentValue = 0;
        }

        return string.Empty;
    }

    private void ResetAllShares()
    {
        foreach (var row in ParticipantRows)
        {
            row.AmountMinor = 0;
            if (!row.IsIncluded || row.Mode != RowSplitMode.Percent)
            {
                row.PercentValue = 0;
            }
        }
    }

    private static void ResetAutoRows(IEnumerable<ParticipantSplitRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            row.AmountMinor = 0;
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
        CanSave = hasAmount && hasPayer && includedCount >= 2 && string.IsNullOrEmpty(StatusText);
        OnPropertyChanged(nameof(CanSave));
    }

    private void RefreshRowsDisplay()
    {
        foreach (var row in ParticipantRows)
        {
            if (!row.IsIncluded)
            {
                row.EditableValue = "—";
                continue;
            }

            row.EditableValue = row.Mode switch
            {
                RowSplitMode.Percent => $"{row.PercentValue}%",
                _ => FormatMinor(row.AmountMinor, _currency)
            };
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
        private RowSplitMode _mode = RowSplitMode.Auto;
        private long _amountMinor;
        private int _percentValue;
        private string _editableValue = "—";

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
                OnPropertyChanged(nameof(IsValueEditable));
            }
        }

        public string IsIncludedMark => _isIncluded ? "✓" : " ";

        public RowSplitMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeChipText));
                OnPropertyChanged(nameof(IsValueEditable));
            }
        }

        public string ModeChipText => _mode switch
        {
            RowSplitMode.Auto => "=",
            RowSplitMode.Exact => "€",
            _ => "%"
        };

        public bool IsValueEditable => _isIncluded && _mode != RowSplitMode.Auto;

        public long AmountMinor
        {
            get => _amountMinor;
            set
            {
                var normalized = Math.Max(0, value);
                if (_amountMinor == normalized)
                {
                    return;
                }

                _amountMinor = normalized;
                OnPropertyChanged();
            }
        }

        public int PercentValue
        {
            get => _percentValue;
            set
            {
                var normalized = Math.Max(0, value);
                if (_percentValue == normalized)
                {
                    return;
                }

                _percentValue = normalized;
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
    }

    public sealed record ImpactRowViewModel(string Text);
}
