using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Pages;

public sealed partial class AddExpenseViewModel : ObservableObject
{
    private readonly IAddExpenseDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private string _currency = "USD";
    private bool _isRecalculating;
    private bool _isSettingParticipantRows;

    public ObservableCollection<string> PayerNames { get; } = new();
    public ObservableCollection<ParticipantSplitRowViewModel> ParticipantRows { get; } = new();
    public ObservableCollection<ImpactRowViewModel> ImpactRows { get; } = new();
    public ObservableCollection<EventIconOptionViewModel> EventIconOptions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTitleInvalid))]
    private string _expenseTitle = string.Empty;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    private string _currencyPrefix = string.Empty;

    [ObservableProperty]
    private string _currencySuffix = string.Empty;

    [ObservableProperty]
    private bool _hasDependents;

    [ObservableProperty]
    private DateTime _expenseDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _expenseTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private string? _selectedPayerName;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _canSave;

    [ObservableProperty]
    private bool _isCalculationValid;

    [ObservableProperty]
    private EventIconOptionViewModel _selectedEventIconOption = GroupPresentationMapper.GetEventIconOptions()[0];

    public bool IsTitleInvalid => string.IsNullOrWhiteSpace(ExpenseTitle);

    public event EventHandler? ExpenseSaved;

    public AddExpenseViewModel(IAddExpenseDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        var defaults = _dataService.GetEventDraftDefaults();
        _currency = overview.Group.Currency;

        var symbol = CurrencyFormatter.GetSymbol(_currency);
        CurrencyPrefix = string.IsNullOrEmpty(symbol) ? string.Empty : symbol;
        CurrencySuffix = string.IsNullOrEmpty(symbol) ? $" {_currency.ToUpperInvariant()}" : string.Empty;

        _participants.Clear();
        _participants.AddRange(overview.Participants);
        PayerNames.Clear();
        ParticipantRows.Clear();
        ImpactRows.Clear();
        _payerParticipantIdByLabel.Clear();
        EventIconOptions.Clear();
        foreach (var option in GroupPresentationMapper.GetEventIconOptions())
        {
            EventIconOptions.Add(option);
        }

        SelectedEventIconOption = GroupPresentationMapper.ResolveEventIconOption(null);

        var adultIds = overview.EconomicUnits
            .Select(u => u.OwnerParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        HasDependents = _participants.Any(p => !adultIds.Contains(p.Id));

        var adults = _participants.Where(p => adultIds.Contains(p.Id)).ToList();
        var dependents = _participants.Where(p => !adultIds.Contains(p.Id)).ToList();

        foreach (var participant in _participants)
        {
            PayerNames.Add(participant.Name);
            _payerParticipantIdByLabel[participant.Name] = participant.Id;
        }

        var isFirstAdult = true;
        var isFirstDependent = true;
        foreach (var participant in adults.Concat(dependents))
        {
            var isAdult = adultIds.Contains(participant.Id);
            string? groupHeader = null;
            if (isAdult && isFirstAdult && HasDependents)
            {
                groupHeader = AppResources.AddEvent_SectionAdults;
            }
            else if (!isAdult && isFirstDependent && HasDependents)
            {
                groupHeader = AppResources.AddEvent_SectionDependents;
                isFirstDependent = false;
            }

            if (isAdult) isFirstAdult = false;

            var isIncluded = defaults.ParticipantIds.Count == 0 || defaults.ParticipantIds.Contains(participant.Id, StringComparer.Ordinal);
            ParticipantRows.Add(new ParticipantSplitRowViewModel(participant.Id, participant.Name, isIncluded)
            {
                GroupHeader = groupHeader,
                IsDependent = !isAdult
            });
        }

        SelectedPayerName = _participants.FirstOrDefault(p => string.Equals(p.Id, defaults.PaidByParticipantId, StringComparison.Ordinal))?.Name
            ?? _participants.FirstOrDefault(p => string.Equals(p.Name, UserProfilePreferences.GetPreferredName(), StringComparison.OrdinalIgnoreCase))?.Name
            ?? PayerNames.FirstOrDefault();

        RecalculateAll();
    }

    [RelayCommand]
    private void SelectAll()
    {
        _isSettingParticipantRows = true;
        try
        {
            foreach (var row in ParticipantRows)
            {
                if (row.IsIncluded) continue;
                row.IsIncluded = true;
                row.SplitMode = SplitMode.Auto;
                row.CommittedPercentage = null;
                row.ValidationError = string.Empty;
                row.HasTransientInvalidInput = false;
                row.RawInput = string.Empty;
            }
        }
        finally
        {
            _isSettingParticipantRows = false;
        }

        RecalculateAll();
    }

    [RelayCommand]
    private void AdultsOnly()
    {
        _isSettingParticipantRows = true;
        try
        {
            foreach (var row in ParticipantRows)
            {
                var shouldBeIncluded = !row.IsDependent;
                if (row.IsIncluded == shouldBeIncluded) continue;
                row.IsIncluded = shouldBeIncluded;
                if (!shouldBeIncluded)
                {
                    row.SplitMode = SplitMode.Auto;
                    row.CommittedPercentage = null;
                    row.ValidationError = string.Empty;
                    row.HasTransientInvalidInput = false;
                    row.RawInput = string.Empty;
                    row.CommittedAmountMinor = 0;
                }
            }
        }
        finally
        {
            _isSettingParticipantRows = false;
        }

        RecalculateAll();
    }

    [RelayCommand]
    private void ToggleParticipant(string participantId)
    {
        var row = ParticipantRows.FirstOrDefault(r => string.Equals(r.Id, participantId, StringComparison.Ordinal));
        if (row is null) return;

        row.IsIncluded = !row.IsIncluded;
        if (!row.IsIncluded)
        {
            row.SplitMode = SplitMode.Auto;
            row.CommittedPercentage = null;
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
            row.RawInput = string.Empty;
            row.CommittedAmountMinor = 0;
        }
        else
        {
            row.SplitMode = SplitMode.Auto;
            row.CommittedPercentage = null;
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
            row.RawInput = string.Empty;
        }

        RecalculateAll();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(ExpenseTitle))
        {
            StatusText = AppResources.Validation_TitleRequired;
            return;
        }

        if (!CanSave || !ExpenseAmountParser.TryParseAmountLenient(AmountText, out var totalMinor))
        {
            return;
        }

        if (SelectedPayerName is null || !_payerParticipantIdByLabel.TryGetValue(SelectedPayerName, out var payerId))
        {
            return;
        }

        try
        {
            var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
            var fixedAmounts = included.ToDictionary(row => row.Id, row => row.CommittedAmountMinor, StringComparer.Ordinal);
            var splitDefinition = new SplitDefinition(new SplitComponent[]
            {
                new FixedSplitComponent(fixedAmounts)
            });

            var title = ExpenseTitle.Trim();
            var expenseDateTime = ExpenseDate.Date.Add(ExpenseTime);
            await _dataService.AddExpenseAsync(
                title,
                totalMinor,
                payerId,
                expenseDateTime,
                included.Select(row => row.Id).ToArray(),
                SelectedEventIconOption.Icon,
                splitDefinition);

            ExpenseSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    /// <summary>Called by code-behind when a participant checkbox fires CheckedChanged.</summary>
    public void OnParticipantCheckedChanged(ParticipantSplitRowViewModel row, bool newValue)
    {
        if (_isSettingParticipantRows) return;

        if (!newValue)
        {
            row.CommittedAmountMinor = 0;
        }

        row.SplitMode = SplitMode.Auto;
        row.CommittedPercentage = null;
        row.ValidationError = string.Empty;
        row.HasTransientInvalidInput = false;
        row.RawInput = string.Empty;

        RecalculateAll();
    }

    /// <summary>Called by code-behind when a participant raw-input entry fires TextChanged.</summary>
    public void OnParticipantRawInputChanged(ParticipantSplitRowViewModel row, string newText)
    {
        if (_isRecalculating) return;
        if (!row.IsIncluded || !row.IsEditing) return;

        row.RawInput = newText;

        if (row.SplitMode == SplitMode.Percentage)
        {
            if (ExpenseAmountParser.IsTransientPercentageAcceptable(row.RawInput, out var parsedPct))
            {
                row.ValidationError = string.Empty;
                row.HasTransientInvalidInput = false;
                if (parsedPct.HasValue)
                {
                    row.CommittedPercentage = parsedPct.Value;
                }
            }
            else
            {
                row.ValidationError = AppResources.Validation_InvalidAmount;
                row.HasTransientInvalidInput = true;
                row.CommittedPercentage = null;
            }
        }
        else
        {
            if (ExpenseAmountParser.IsTransientInputAcceptable(row.RawInput, out var parsedMinor))
            {
                row.ValidationError = string.Empty;
                row.HasTransientInvalidInput = false;
                if (parsedMinor.HasValue)
                {
                    row.CommittedAmountMinor = parsedMinor.Value;
                }
            }
            else
            {
                row.ValidationError = AppResources.Validation_InvalidAmount;
                row.HasTransientInvalidInput = true;
            }
        }

        RecalculateAll();
    }

    /// <summary>Called by code-behind after the user picks a split mode from the action sheet.</summary>
    public void ApplyModeChange(string participantId, SplitMode newMode)
    {
        var row = ParticipantRows.FirstOrDefault(r => string.Equals(r.Id, participantId, StringComparison.Ordinal));
        if (row is null || !row.IsIncluded) return;

        var previousAmount = row.CommittedAmountMinor;
        row.ValidationError = string.Empty;
        row.HasTransientInvalidInput = false;
        row.CommittedPercentage = null;

        switch (newMode)
        {
            case SplitMode.Fixed:
                row.RawInput = previousAmount > 0
                    ? (previousAmount / 100m).ToString("0.##", CultureInfo.CurrentCulture)
                    : string.Empty;
                row.SplitMode = SplitMode.Fixed;
                break;
            case SplitMode.Percentage:
                row.RawInput = string.Empty;
                row.CommittedAmountMinor = 0;
                row.SplitMode = SplitMode.Percentage;
                break;
            default:
                row.RawInput = string.Empty;
                row.CommittedAmountMinor = 0;
                row.SplitMode = SplitMode.Auto;
                break;
        }

        RecalculateAll();
    }

    /// <summary>Called by code-behind after a media/photo pick attempt to surface a status message.</summary>
    public void SetMediaStatus(string status)
    {
        StatusText = status;
    }

    partial void OnExpenseTitleChanged(string value)
    {
        RecalculateSaveState();
    }

    partial void OnAmountTextChanged(string value)
    {
        RecalculateAll();
    }

    partial void OnSelectedPayerNameChanged(string? value)
    {
        RecalculateImpact();
        RecalculateSaveState();
    }

    private void RecalculateAll()
    {
        _isRecalculating = true;
        try
        {
            var validationMessage = AddExpenseSplitCalculations.ComputeRows(
                ParticipantRows,
                AmountText,
                AppResources.Validation_InvalidAmount,
                AppResources.Validation_PickAtLeastOnePerson);

            StatusText = validationMessage;
            IsCalculationValid = string.IsNullOrEmpty(validationMessage);

            if (IsCalculationValid)
            {
                RecalculateImpact();
            }
            else
            {
                ImpactRows.Clear();
            }

            RecalculateSaveState();
            RefreshRowsDisplay();
        }
        finally
        {
            _isRecalculating = false;
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
            if (row.CommittedAmountMinor <= 0) continue;
            ImpactRows.Add(new ImpactRowViewModel($"{row.Name} → {SelectedPayerName} {CurrencyFormatter.FormatMinor(row.CommittedAmountMinor, _currency)}"));
            if (ImpactRows.Count >= 4) break;
        }
    }

    private void RecalculateSaveState()
    {
        var hasAmount = ExpenseAmountParser.TryParseAmountLenient(AmountText, out _);
        var hasTitle = !string.IsNullOrWhiteSpace(ExpenseTitle);
        var hasPayer = SelectedPayerName is not null && _payerParticipantIdByLabel.ContainsKey(SelectedPayerName);
        var includedCount = ParticipantRows.Count(row => row.IsIncluded);
        var hasRowInputError = ParticipantRows.Any(row => row.HasTransientInvalidInput);

        CanSave = hasTitle
            && hasAmount
            && hasPayer
            && includedCount >= 2
            && !hasRowInputError
            && IsCalculationValid
            && string.IsNullOrEmpty(StatusText);
    }

    private void RefreshRowsDisplay()
    {
        foreach (var row in ParticipantRows)
        {
            if (!row.IsIncluded)
            {
                row.DisplayValue = "—";
                continue;
            }

            if (row.SplitMode == SplitMode.Fixed)
            {
                if (string.IsNullOrWhiteSpace(row.RawInput))
                {
                    row.RawInput = (row.CommittedAmountMinor / 100m).ToString("0.##", CultureInfo.CurrentCulture);
                }

                row.DisplayValue = string.Empty;
            }
            else if (row.SplitMode == SplitMode.Percentage)
            {
                row.DisplayValue = string.Empty;
            }
            else
            {
                row.DisplayValue = CurrencyFormatter.FormatMinor(row.CommittedAmountMinor, _currency);
            }
        }
    }
}
