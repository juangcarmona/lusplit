using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Pages;

public partial class ExpenseDetailsPage : ContentPage, IQueryAttributable
{
    private readonly AppDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private string _expenseId = string.Empty;
    private string _currency = "USD";
    private bool _isEditMode;
    private long _fixedTotalMinor;

    public ObservableCollection<ExpenseParticipantRowViewModel> ParticipantRows { get; } = new();
    public ObservableCollection<PreviewRowViewModel> PreviewRows { get; } = new();
    public ObservableCollection<string> PayerNames { get; } = new();

    public string ExpenseTitle { get; set; } = string.Empty;
    public string HeaderLine1 { get; private set; } = string.Empty;
    public string HeaderLine2 { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public string NoteText { get; private set; } = string.Empty;
    public bool HasNote => !string.IsNullOrWhiteSpace(NoteText);
    public string? SelectedPayerName { get; set; }
    public string StatusText { get; private set; } = string.Empty;
    public bool CanSave { get; private set; }
    public bool IsEditMode => _isEditMode;
    public bool IsViewMode => !_isEditMode;
    public string EditButtonText => _isEditMode ? "Cancel" : "Edit";
    public string ExpectedTotalText => $"Total fixed: {FormatMinor(_fixedTotalMinor, _currency)}";

    public ExpenseDetailsPage(AppDataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();
        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _expenseId = query.TryGetValue("expenseId", out var expenseId) ? expenseId?.ToString() ?? string.Empty : string.Empty;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var overview = await _dataService.GetOverviewAsync();
        _currency = overview.Group.Currency;
        _participants.Clear();
        _participants.AddRange(overview.Participants);
        PayerNames.Clear();
        foreach (var participant in _participants)
        {
            PayerNames.Add(participant.Name);
        }

        var expense = await _dataService.GetExpenseAsync(_expenseId);
        if (expense is null)
        {
            StatusText = "Expense not found.";
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        ExpenseTitle = expense.Title;
        _fixedTotalMinor = expense.AmountMinor;
        HeaderLine1 = $"{expense.Title} — {FormatMinor(expense.AmountMinor, _currency)}";

        var payerName = _participants.FirstOrDefault(participant => string.Equals(participant.Id, expense.PaidByParticipantId, StringComparison.Ordinal))?.Name ?? "Unknown";
        var participantCount = expense.SplitDefinition.Components
            .SelectMany(component => component switch
            {
                FixedSplitComponent fixedComponent => fixedComponent.Shares.Keys,
                RemainderSplitComponent remainderComponent => remainderComponent.Participants,
                _ => Array.Empty<string>()
            })
            .Distinct(StringComparer.Ordinal)
            .Count();

        HeaderLine2 = $"{payerName} → {participantCount} people";
        DateText = expense.Date;
        NoteText = expense.Notes ?? string.Empty;
        SelectedPayerName = payerName;

        BuildParticipantRows(expense, payerName);
        RebuildPreviewRows();
        RecalculateSaveState();

        OnPropertyChanged(nameof(ExpenseTitle));
        OnPropertyChanged(nameof(HeaderLine1));
        OnPropertyChanged(nameof(HeaderLine2));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(SelectedPayerName));
        OnPropertyChanged(nameof(ExpectedTotalText));
    }

    private void BuildParticipantRows(ExpenseModel expense, string payerName)
    {
        ParticipantRows.Clear();
        var fixedComponent = expense.SplitDefinition.Components.OfType<FixedSplitComponent>().FirstOrDefault();
        var shares = fixedComponent?.Shares ?? new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var participant in _participants)
        {
            if (!shares.TryGetValue(participant.Id, out var amountMinor) || amountMinor <= 0)
            {
                continue;
            }

            ParticipantRows.Add(new ExpenseParticipantRowViewModel(
                participant.Id,
                participant.Name,
                string.Equals(participant.Name, payerName, StringComparison.Ordinal),
                amountMinor,
                _currency));
        }
    }

    private void RebuildPreviewRows()
    {
        PreviewRows.Clear();
        var payerName = SelectedPayerName ?? string.Empty;
        foreach (var row in ParticipantRows.Where(row => !row.IsPayer && row.IsIncluded && row.AmountMinor > 0))
        {
            PreviewRows.Add(new PreviewRowViewModel($"{row.Name} → {payerName} {FormatMinor(row.AmountMinor, _currency)}"));
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void OnEditClicked(object? sender, EventArgs e)
    {
        _isEditMode = !_isEditMode;
        foreach (var row in ParticipantRows)
        {
            row.IsEditMode = _isEditMode;
            row.IsEditing = false;
        }

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsViewMode));
        OnPropertyChanged(nameof(EditButtonText));
        RecalculateSaveState();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Delete expense", "Are you sure you want to delete this expense?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _dataService.DeleteExpenseAsync(_expenseId);
        await Shell.Current.GoToAsync("..");
    }

    private void OnParticipantToggleTapped(object? sender, TappedEventArgs e)
    {
        if (!_isEditMode || e.Parameter is not string id)
        {
            return;
        }

        var row = ParticipantRows.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (row is null || row.IsPayer)
        {
            return;
        }

        row.IsIncluded = !row.IsIncluded;
        RebuildPreviewRows();
        RecalculateSaveState();
    }

    private void OnParticipantEditClicked(object? sender, EventArgs e)
    {
        if (!_isEditMode || sender is not Button { CommandParameter: string id })
        {
            return;
        }

        foreach (var row in ParticipantRows)
        {
            row.IsEditing = string.Equals(row.Id, id, StringComparison.Ordinal) && row.IsIncluded;
        }
    }

    private void OnParticipantRawInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isEditMode || sender is not Entry { BindingContext: ExpenseParticipantRowViewModel row })
        {
            return;
        }

        row.RawInput = e.NewTextValue ?? string.Empty;
        if (TryParseAmount(row.RawInput, out var minor))
        {
            row.AmountMinor = minor;
            row.IsEditing = false;
        }

        RebuildPreviewRows();
        RecalculateSaveState();
    }

    private void OnExpenseTitleChanged(object? sender, TextChangedEventArgs e)
    {
        ExpenseTitle = e.NewTextValue ?? string.Empty;
        RecalculateSaveState();
    }

    private void OnPayerChanged(object? sender, EventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        foreach (var row in ParticipantRows)
        {
            row.IsPayer = string.Equals(row.Name, SelectedPayerName, StringComparison.Ordinal);
        }

        RebuildPreviewRows();
        RecalculateSaveState();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!_isEditMode || !CanSave || string.IsNullOrWhiteSpace(SelectedPayerName))
        {
            return;
        }

        var payer = _participants.FirstOrDefault(participant => string.Equals(participant.Name, SelectedPayerName, StringComparison.Ordinal));
        if (payer is null)
        {
            return;
        }

        var shares = ParticipantRows
            .Where(row => row.IsIncluded)
            .ToDictionary(row => row.Id, row => row.AmountMinor, StringComparer.Ordinal);

        var total = shares.Values.Sum();
        if (total != _fixedTotalMinor)
        {
            StatusText = "Total must match amount";
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        var splitDefinition = new SplitDefinition(new SplitComponent[] { new FixedSplitComponent(shares) });

        await _dataService.UpdateExpenseAsync(
            _expenseId,
            ExpenseTitle.Trim(),
            payer.Id,
            _fixedTotalMinor,
            DateTime.TryParse(DateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate)
                ? parsedDate
                : DateTime.Today,
            splitDefinition,
            string.IsNullOrWhiteSpace(NoteText) ? null : NoteText);

        _isEditMode = false;
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsViewMode));
        OnPropertyChanged(nameof(EditButtonText));
        await LoadAsync();
    }

    private void RecalculateSaveState()
    {
        var totalMinor = ParticipantRows.Where(row => row.IsIncluded).Sum(row => row.AmountMinor);
        var hasIncluded = ParticipantRows.Any(row => row.IsIncluded);
        var hasPayer = !string.IsNullOrWhiteSpace(SelectedPayerName);
        var hasTitle = !string.IsNullOrWhiteSpace(ExpenseTitle);
        var totalMatches = totalMinor == _fixedTotalMinor;
        StatusText = _isEditMode && !totalMatches
            ? "Total must match amount"
            : string.Empty;
        OnPropertyChanged(nameof(StatusText));
        CanSave = _isEditMode && hasTitle && hasIncluded && hasPayer && totalMinor > 0 && totalMatches;
        OnPropertyChanged(nameof(CanSave));
    }

    private static bool TryParseAmount(string text, out long amountMinor)
    {
        amountMinor = 0;
        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariant)
            || decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out invariant))
        {
            amountMinor = (long)Math.Round(invariant * 100m, MidpointRounding.AwayFromZero);
            return amountMinor >= 0;
        }

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
}

public sealed class ExpenseParticipantRowViewModel : BindableObject
{
    private bool _isPayer;
    private bool _isIncluded = true;
    private bool _isEditMode;
    private bool _isEditing;
    private long _amountMinor;
    private string _rawInput = string.Empty;
    private readonly string _currency;

    public string Id { get; }
    public string Name { get; }

    public bool IsPayer
    {
        get => _isPayer;
        set
        {
            if (_isPayer == value) return;
            _isPayer = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NameAndPayer));
            OnPropertyChanged(nameof(CanEditAmount));
        }
    }

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded == value) return;
            _isIncluded = value;
            if (!_isIncluded)
            {
                AmountMinor = 0;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectMark));
            OnPropertyChanged(nameof(CanEditAmount));
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode == value) return;
            _isEditMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditAmount));
            OnPropertyChanged(nameof(IsViewing));
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsViewing));
        }
    }

    public long AmountMinor
    {
        get => _amountMinor;
        set
        {
            if (_amountMinor == value) return;
            _amountMinor = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AmountText));
        }
    }

    public string RawInput
    {
        get => _rawInput;
        set
        {
            if (string.Equals(_rawInput, value, StringComparison.Ordinal)) return;
            _rawInput = value;
            OnPropertyChanged();
        }
    }

    public string SelectMark => IsIncluded ? "✓" : " ";
    public string NameAndPayer => IsPayer ? $"{Name} (payer)" : Name;
    public string AmountText => FormatMinor(AmountMinor, _currency);
    public bool IsViewing => !_isEditing;
    public bool CanEditAmount => IsEditMode && IsIncluded && !IsPayer;

    public ExpenseParticipantRowViewModel(string id, string name, bool isPayer, long amountMinor, string currency)
    {
        Id = id;
        Name = name;
        _isPayer = isPayer;
        _amountMinor = amountMinor;
        _currency = currency;
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
}

public sealed record PreviewRowViewModel(string Text);
