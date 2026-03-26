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
        var symbol = _currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => string.Empty
        };
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

            if (!CanSave || !TryParseAmountLenient(AmountText, out var totalMinor))
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
                row.IsEditing = false;
                row.ValidationError = string.Empty;
                row.HasTransientInvalidInput = false;
                row.RawInput = string.Empty;
                row.IsCustomAmount = false;
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
                    row.IsEditing = false;
                    row.ValidationError = string.Empty;
                    row.HasTransientInvalidInput = false;
                    row.RawInput = string.Empty;
                    row.CommittedAmountMinor = 0;
                    row.IsCustomAmount = false;
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
            row.IsEditing = false;
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
            row.RawInput = string.Empty;
            row.CommittedAmountMinor = 0;
            row.IsCustomAmount = false;
        }
        else
        {
            row.IsEditing = false;
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
            row.RawInput = string.Empty;
            row.IsCustomAmount = false;
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

        row.IsEditing = false;
        row.ValidationError = string.Empty;
        row.HasTransientInvalidInput = false;
        row.RawInput = string.Empty;
        row.IsCustomAmount = false;

        RecalculateAll();
    }

    private async void OnParticipantEditStartClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string participantId })
        {
            return;
        }

        foreach (var item in ParticipantRows)
        {
            if (!string.Equals(item.Id, participantId, StringComparison.Ordinal))
            {
                item.IsEditing = false;
                item.ValidationError = string.Empty;
            }
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        if (row is null || !row.IsIncluded)
        {
            return;
        }

        row.IsEditing = true;
        row.RawInput = (row.CommittedAmountMinor / 100m).ToString("0.##", CultureInfo.CurrentCulture);
        row.ValidationError = string.Empty;
        row.HasTransientInvalidInput = false;
        RefreshRowsDisplay();
        await Task.CompletedTask;
    }

    private void OnParticipantEditConfirmClicked(object? sender, EventArgs e)
    {
        ParticipantSplitRowViewModel? row = null;

        if (sender is Entry { BindingContext: ParticipantSplitRowViewModel fromEntry })
        {
            row = fromEntry;
        }
        else if (sender is Button { CommandParameter: string participantId })
        {
            row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, participantId, StringComparison.Ordinal));
        }

        if (row is null || !row.IsIncluded || !row.IsEditing)
        {
            return;
        }

        if (TryParseAmountLenient(row.RawInput, out var parsedMinor))
        {
            row.CommittedAmountMinor = parsedMinor;
            row.IsCustomAmount = true;
            row.IsEditing = false;
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
        }
        else
        {
            row.ValidationError = AppResources.Validation_InvalidAmount;
            row.HasTransientInvalidInput = true;
        }

        RecalculateAll();
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

        if (IsTransientInputAcceptable(row.RawInput, out var parsedMinor))
        {
            row.ValidationError = string.Empty;
            row.HasTransientInvalidInput = false;
            row.LiveParsedAmountMinor = parsedMinor;
        }
        else
        {
            row.ValidationError = AppResources.Validation_InvalidAmount;
            row.HasTransientInvalidInput = true;
            row.LiveParsedAmountMinor = null;
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
        if (!TryParseAmountLenient(AmountText, out var totalMinor))
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

        var isProvisionalCustom = (ParticipantSplitRowViewModel row) => row.IsCustomAmount || (row.IsEditing && row.LiveParsedAmountMinor.HasValue);
        var customRows = included.Where(row => isProvisionalCustom(row)).ToArray();
        var autoRows = included.Where(row => !isProvisionalCustom(row)).ToArray();

        var customSum = customRows.Sum(row => row.IsCustomAmount ? row.CommittedAmountMinor : (row.LiveParsedAmountMinor ?? 0));
        var remaining = totalMinor - customSum;
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
            row.LiveParsedAmountMinor = null;
        }

        return string.Empty;
    }

    private void ResetAllCommittedShares()
    {
        foreach (var row in ParticipantRows)
        {
            row.CommittedAmountMinor = 0;
            row.LiveParsedAmountMinor = null;
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

            ImpactRows.Add(new ImpactRowViewModel($"{row.Name} → {SelectedPayerName} {FormatMinor(row.CommittedAmountMinor, _currency)}"));
            if (ImpactRows.Count >= 4)
            {
                break;
            }
        }
    }

    private void RecalculateSaveState()
    {
        var hasAmount = TryParseAmountLenient(AmountText, out _);
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

            if (row.IsEditing)
            {
                if (string.IsNullOrWhiteSpace(row.RawInput))
                {
                    row.RawInput = (row.CommittedAmountMinor / 100m).ToString("0.##", CultureInfo.CurrentCulture);
                }

                row.DisplayValue = string.Empty;
            }
            else
            {
                row.DisplayValue = FormatMinor(row.CommittedAmountMinor, _currency);
            }
        }
    }

    private static bool IsTransientInputAcceptable(string? input, out long? parsedMinor)
    {
        parsedMinor = null;
        var normalized = NormalizeNumberInput(input);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        var trailingDecimal = normalized.EndsWith(".", StringComparison.Ordinal);
        if (normalized == ".")
        {
            return true;
        }

        if (trailingDecimal)
        {
            normalized = normalized.TrimEnd('.');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }
        }

        if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0)
            {
                return false;
            }

            parsedMinor = (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
            return true;
        }

        return false;
    }

    private static bool TryParseAmountLenient(string? text, out long amountMinor)
    {
        amountMinor = 0;
        var normalized = NormalizeNumberInput(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized == "." || normalized.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        if (decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            amountMinor = (long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
            return amountMinor > 0;
        }

        return false;
    }

    private static string NormalizeNumberInput(string? text)
    {
        var value = (text ?? string.Empty)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (value.Contains(',') && value.Contains('.'))
        {
            // Keep the last separator as decimal separator and strip the other separator.
            var lastComma = value.LastIndexOf(',');
            var lastDot = value.LastIndexOf('.');
            if (lastComma > lastDot)
            {
                value = value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            }
            else
            {
                value = value.Replace(",", string.Empty, StringComparison.Ordinal);
            }
        }
        else if (value.Contains(','))
        {
            value = value.Replace(',', '.');
        }

        return value;
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
        private bool _isEditing;
        private bool _isCustomAmount;
        private string _rawInput = string.Empty;
        private long _committedAmountMinor;
        private long? _liveParsedAmountMinor;
        private string _validationError = string.Empty;
        private bool _hasTransientInvalidInput;
        private string _displayValue = "—";

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
                OnPropertyChanged(nameof(CanShowEditButton));
                OnPropertyChanged(nameof(IsViewing));
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing == value)
                {
                    return;
                }

                _isEditing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanShowEditButton));
                OnPropertyChanged(nameof(IsViewing));
            }
        }

        public bool IsCustomAmount
        {
            get => _isCustomAmount;
            set
            {
                if (_isCustomAmount == value)
                {
                    return;
                }

                _isCustomAmount = value;
                OnPropertyChanged();
            }
        }

        public string RawInput
        {
            get => _rawInput;
            set
            {
                if (string.Equals(_rawInput, value, StringComparison.Ordinal))
                {
                    return;
                }

                _rawInput = value;
                OnPropertyChanged();
            }
        }

        public long CommittedAmountMinor
        {
            get => _committedAmountMinor;
            set
            {
                var normalized = Math.Max(0, value);
                if (_committedAmountMinor == normalized)
                {
                    return;
                }

                _committedAmountMinor = normalized;
                OnPropertyChanged();
            }
        }

        public long? LiveParsedAmountMinor
        {
            get => _liveParsedAmountMinor;
            set
            {
                if (_liveParsedAmountMinor == value)
                {
                    return;
                }

                _liveParsedAmountMinor = value;
                OnPropertyChanged();
            }
        }

        public string ValidationError
        {
            get => _validationError;
            set
            {
                if (string.Equals(_validationError, value, StringComparison.Ordinal))
                {
                    return;
                }

                _validationError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationError));
            }
        }

        public bool HasTransientInvalidInput
        {
            get => _hasTransientInvalidInput;
            set
            {
                if (_hasTransientInvalidInput == value)
                {
                    return;
                }

                _hasTransientInvalidInput = value;
                OnPropertyChanged();
            }
        }

        public string DisplayValue
        {
            get => _displayValue;
            set
            {
                if (string.Equals(_displayValue, value, StringComparison.Ordinal))
                {
                    return;
                }

                _displayValue = value;
                OnPropertyChanged();
            }
        }

        public string IsIncludedMark => _isIncluded ? "✓" : " ";
        public bool CanShowEditButton => _isIncluded && !_isEditing;
        public bool IsViewing => !_isEditing;
        public bool HasValidationError => !string.IsNullOrWhiteSpace(_validationError);

        public string? GroupHeader { get; set; }
        public bool HasGroupHeader => !string.IsNullOrEmpty(GroupHeader);
        public bool IsDependent { get; set; }

        public ParticipantSplitRowViewModel(string id, string name, bool isIncluded)
        {
            Id = id;
            Name = name;
            _isIncluded = isIncluded;
        }
    }

    public sealed record ImpactRowViewModel(string Text);
}
