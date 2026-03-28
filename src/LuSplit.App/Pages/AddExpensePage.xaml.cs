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
    private readonly Dictionary<string, string> _payerParticipantIdByLabel = new(StringComparer.Ordinal);
    private string _currency = "USD";
    private string _currencyPrefix = string.Empty;
    private string _currencySuffix = string.Empty;
    private bool _isRecalculating;
    private bool _isSettingParticipantRows;
    private bool _isCalculationValid;
    private EventIconOptionViewModel _selectedEventIconOption = GroupPresentationMapper.GetEventIconOptions()[0];

    public ObservableCollection<string> PayerNames { get; } = new();
    public ObservableCollection<ParticipantSplitRowViewModel> ParticipantRows { get; } = new();
    public ObservableCollection<ImpactRowViewModel> ImpactRows { get; } = new();
    public ObservableCollection<EventIconOptionViewModel> EventIconOptions { get; } = new();

    public string ExpenseTitle { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public string CurrencyPrefix => _currencyPrefix;
    public string CurrencySuffix => _currencySuffix;
    public bool HasDependents { get; private set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public TimeSpan ExpenseTime { get; set; } = DateTime.Now.TimeOfDay;
    public string? SelectedPayerName { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public bool CanSave { get; private set; }
    public bool IsTitleInvalid => string.IsNullOrWhiteSpace(ExpenseTitle);
    public EventIconOptionViewModel SelectedEventIconOption
    {
        get => _selectedEventIconOption;
        set
        {
            if (_selectedEventIconOption == value)
            {
                return;
            }

            _selectedEventIconOption = value;
            OnPropertyChanged();
        }
    }
    public bool IsCalculationValid
    {
        get => _isCalculationValid;
        private set
        {
            if (_isCalculationValid == value)
            {
                return;
            }

            _isCalculationValid = value;
            OnPropertyChanged(nameof(IsCalculationValid));
        }
    }

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

        // Currency prefix/suffix for display
        var symbol = CurrencyFormatter.GetSymbol(_currency);
        _currencyPrefix = string.IsNullOrEmpty(symbol) ? string.Empty : symbol;
        _currencySuffix = string.IsNullOrEmpty(symbol) ? $" {_currency.ToUpperInvariant()}" : string.Empty;
        OnPropertyChanged(nameof(CurrencyPrefix));
        OnPropertyChanged(nameof(CurrencySuffix));

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
        OnPropertyChanged(nameof(SelectedEventIconOption));

        // Determine adults vs. dependents from economic units
        var adultIds = overview.EconomicUnits
            .Select(u => u.OwnerParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        HasDependents = _participants.Any(p => !adultIds.Contains(p.Id));
        OnPropertyChanged(nameof(HasDependents));

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
            if (string.IsNullOrWhiteSpace(ExpenseTitle))
            {
                StatusText = AppResources.Validation_TitleRequired;
                OnPropertyChanged(nameof(StatusText));
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

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e) => RecalculateAll();

    private void OnSelectAllClicked(object? sender, EventArgs e)
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

    private void OnAdultsOnlyClicked(object? sender, EventArgs e)
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
    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsTitleInvalid));
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

    private void OnParticipantCheckChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isSettingParticipantRows)
        {
            return;
        }

        if (sender is not CheckBox { BindingContext: ParticipantSplitRowViewModel row })
        {
            return;
        }

        if (!row.IsIncluded)
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

    private async void OnModeSelectorClicked(object? sender, EventArgs e)
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

        var result = await DisplayActionSheet(
            AppResources.AddEvent_SplitMode_Title,
            AppResources.Common_Cancel,
            null,
            AppResources.AddEvent_SplitMode_Auto,
            AppResources.AddEvent_SplitMode_Fixed,
            AppResources.AddEvent_SplitMode_Percentage);

        if (result is null || string.Equals(result, AppResources.Common_Cancel, StringComparison.Ordinal))
        {
            return;
        }

        SplitMode newMode;
        if (string.Equals(result, AppResources.AddEvent_SplitMode_Fixed, StringComparison.Ordinal))
        {
            newMode = SplitMode.Fixed;
        }
        else if (string.Equals(result, AppResources.AddEvent_SplitMode_Percentage, StringComparison.Ordinal))
        {
            newMode = SplitMode.Percentage;
        }
        else
        {
            newMode = SplitMode.Auto;
        }

        ApplyModeChange(row, newMode);
        RecalculateAll();
    }

    private void ApplyModeChange(ParticipantSplitRowViewModel row, SplitMode newMode)
    {
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
    }

    private void OnParticipantRawInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isRecalculating)
        {
            return;
        }

        if (sender is not Entry { BindingContext: ParticipantSplitRowViewModel row } || !row.IsIncluded || !row.IsEditing)
        {
            return;
        }

        row.RawInput = e.NewTextValue ?? string.Empty;

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

    private async void OnAttachMediaClicked(object? sender, EventArgs e)
    {
        try
        {
            var pickOptions = new PickOptions
            {
                PickerTitle = AppResources.AddEvent_AttachMedia
            };

            var file = await FilePicker.Default.PickAsync(pickOptions);
            if (file is null)
            {
                StatusText = AppResources.Common_Cancel;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            StatusText = AppResources.AddEvent_AttachMediaQueued;
            OnPropertyChanged(nameof(StatusText));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var cameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraPermission != PermissionStatus.Granted)
            {
                cameraPermission = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (cameraPermission != PermissionStatus.Granted)
            {
                StatusText = AppResources.Common_Cancel;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                StatusText = AppResources.AddEvent_CameraNotSupported;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
            {
                StatusText = AppResources.Common_Cancel;
                OnPropertyChanged(nameof(StatusText));
                return;
            }

            StatusText = AppResources.AddEvent_TakePhotoQueued;
            OnPropertyChanged(nameof(StatusText));
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void RecalculateAll()
    {
        _isRecalculating = true;
        try
        {
            var validationMessage = ValidateAndComputeRows();
            StatusText = validationMessage;
            OnPropertyChanged(nameof(StatusText));
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

    private string ValidateAndComputeRows()
    {
        if (!ExpenseAmountParser.TryParseAmountLenient(AmountText, out var totalMinor))
        {
            ResetAllCommittedShares();
            return AppResources.Validation_InvalidAmount;
        }

        var included = ParticipantRows.Where(row => row.IsIncluded).ToArray();
        if (included.Length < 2)
        {
            ResetAllCommittedShares();
            return AppResources.Validation_PickAtLeastOnePerson;
        }

        if (included.Any(row => row.HasTransientInvalidInput))
        {
            return AppResources.Validation_InvalidAmount;
        }

        var isEffectivelyFixed = (ParticipantSplitRowViewModel row) =>
            row.SplitMode == SplitMode.Fixed && !string.IsNullOrWhiteSpace(row.RawInput) && !row.HasTransientInvalidInput;

        var isEffectivelyPercentage = (ParticipantSplitRowViewModel row) =>
            row.SplitMode == SplitMode.Percentage && !string.IsNullOrWhiteSpace(row.RawInput) && !row.HasTransientInvalidInput && row.CommittedPercentage.HasValue;

        var fixedRows = included.Where(r => isEffectivelyFixed(r)).ToArray();
        var pctRows = included.Where(r => isEffectivelyPercentage(r)).ToArray();
        var autoRows = included.Where(r => !isEffectivelyFixed(r) && !isEffectivelyPercentage(r)).ToArray();

        var fixedSum = fixedRows.Sum(r => r.CommittedAmountMinor);
        foreach (var row in pctRows)
        {
            row.CommittedAmountMinor = (long)Math.Round(totalMinor * row.CommittedPercentage!.Value / 100m, MidpointRounding.AwayFromZero);
        }

        var pctSum = pctRows.Sum(r => r.CommittedAmountMinor);
        var remaining = totalMinor - fixedSum - pctSum;
        if (remaining < 0)
        {
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
                autoRows[index].CommittedAmountMinor = baseShare + (index < remainder ? 1 : 0);
            }
        }

        foreach (var row in ParticipantRows.Where(row => !row.IsIncluded))
        {
            row.CommittedAmountMinor = 0;
        }

        return string.Empty;
    }

    private void ResetAllCommittedShares()
    {
        foreach (var row in ParticipantRows)
        {
            row.CommittedAmountMinor = 0;
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
            if (row.CommittedAmountMinor <= 0)
            {
                continue;
            }

            ImpactRows.Add(new ImpactRowViewModel($"{row.Name} → {SelectedPayerName} {CurrencyFormatter.FormatMinor(row.CommittedAmountMinor, _currency)}"));
            if (ImpactRows.Count >= 4)
            {
                break;
            }
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
        OnPropertyChanged(nameof(CanSave));
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
                // RawInput holds the percentage string; leave as-is
                row.DisplayValue = string.Empty;
            }
            else
            {
                row.DisplayValue = CurrencyFormatter.FormatMinor(row.CommittedAmountMinor, _currency);
            }
        }
    }

}
