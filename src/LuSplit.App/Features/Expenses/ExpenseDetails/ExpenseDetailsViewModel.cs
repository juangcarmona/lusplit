using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Formatting;
using LuSplit.Application.Groups.Models;
using LuSplit.Domain.Expenses;
using System.Collections.ObjectModel;
using System.Globalization;

namespace LuSplit.App.Pages;

public sealed partial class ExpenseDetailsViewModel : ObservableObject
{
    private readonly IExpenseDetailsDataService _dataService;
    private readonly List<ParticipantModel> _participants = new();
    private string _expenseId = string.Empty;
    private string? _contextGroupId;
    private string _currency = "USD";
    private long _fixedTotalMinor;

    public ObservableCollection<ExpenseParticipantRowViewModel> ParticipantRows { get; } = new();
    public ObservableCollection<PreviewRowViewModel> PreviewRows { get; } = new();
    public ObservableCollection<string> PayerNames { get; } = new();

    [ObservableProperty]
    private string _expenseTitle = string.Empty;

    [ObservableProperty]
    private string _headerLine1 = string.Empty;

    [ObservableProperty]
    private string _headerLine2 = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string _noteText = string.Empty;

    [ObservableProperty]
    private string? _selectedPayerName;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _canSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewMode))]
    [NotifyPropertyChangedFor(nameof(EditButtonText))]
    private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(ShowArchivedBanner))]
    private bool _isArchived;

    public bool HasNote => !string.IsNullOrWhiteSpace(NoteText);
    public bool IsViewMode => !IsEditMode;
    public bool IsReadOnly => IsArchived;
    public bool CanEdit => !IsArchived;
    public bool CanDelete => !IsArchived;
    public bool ShowArchivedBanner => IsArchived;
    public string EditButtonText => IsEditMode ? AppResources.Common_Cancel : AppResources.Common_Edit;
    public string ExpectedTotalText => string.Format(AppResources.ExpenseDetails_TotalFixed, CurrencyFormatter.FormatMinor(_fixedTotalMinor, _currency));

    public event EventHandler? ExpenseDeleted;

    public ExpenseDetailsViewModel(IExpenseDetailsDataService dataService)
    {
        _dataService = dataService;
    }

    public void SetExpenseId(string expenseId) => _expenseId = expenseId;

    public void SetGroupId(string groupId) => _contextGroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;

    public async Task LoadAsync()
    {
        GroupOverviewModel overview;
        ExpenseModel? expense;

        if (!string.IsNullOrWhiteSpace(_contextGroupId))
        {
            expense = await _dataService.GetExpenseAsync(_expenseId, _contextGroupId);
            overview = await _dataService.GetOverviewAsync(_contextGroupId);
        }
        else
        {
            overview = await _dataService.GetOverviewAsync();
            expense = await _dataService.GetExpenseAsync(_expenseId);
        }

        IsArchived = overview.Group.Closed;
        _currency = overview.Group.Currency;
        _participants.Clear();
        _participants.AddRange(overview.Participants);
        PayerNames.Clear();
        foreach (var participant in _participants)
            PayerNames.Add(participant.Name);
        if (expense is null)
        {
            StatusText = AppResources.ExpenseDetails_NotFound;
            return;
        }

        ExpenseTitle = expense.Title;
        _fixedTotalMinor = expense.AmountMinor;
        HeaderLine1 = $"{expense.Title} - {CurrencyFormatter.FormatMinor(expense.AmountMinor, _currency)}";

        var payerName = _participants
            .FirstOrDefault(p => string.Equals(p.Id, expense.PaidByParticipantId, StringComparison.Ordinal))
            ?.Name ?? AppResources.Common_Unknown;

        var participantCount = expense.SplitDefinition.Components
            .SelectMany(c => c switch
            {
                FixedSplitComponent f => f.Shares.Keys,
                RemainderSplitComponent r => r.Participants,
                _ => Array.Empty<string>()
            })
            .Distinct(StringComparer.Ordinal)
            .Count();

        HeaderLine2 = string.Format(AppResources.Mapper_PeopleCountFormat, payerName, participantCount);
        DateText = expense.Date;
        NoteText = expense.Notes ?? string.Empty;
        SelectedPayerName = payerName;

        BuildParticipantRows(expense, payerName);
        RebuildPreviewRows();
        RecalculateSaveState();
        OnPropertyChanged(nameof(ExpectedTotalText));
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (IsArchived) return;
        IsEditMode = !IsEditMode;
        foreach (var row in ParticipantRows)
        {
            row.IsEditMode = IsEditMode;
            row.IsEditing = false;
        }

        RecalculateSaveState();
    }

    [RelayCommand]
    private void ToggleParticipant(string id)
    {
        if (!IsEditMode) return;

        var row = ParticipantRows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (row is null || row.IsPayer) return;

        row.IsIncluded = !row.IsIncluded;
        RebuildPreviewRows();
        RecalculateSaveState();
    }

    [RelayCommand]
    private void ParticipantEdit(string id)
    {
        if (!IsEditMode) return;

        foreach (var row in ParticipantRows)
            row.IsEditing = string.Equals(row.Id, id, StringComparison.Ordinal) && row.IsIncluded;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!IsEditMode || !CanSave || string.IsNullOrWhiteSpace(SelectedPayerName)) return;

        var payer = _participants.FirstOrDefault(p => string.Equals(p.Name, SelectedPayerName, StringComparison.Ordinal));
        if (payer is null) return;

        var shares = ParticipantRows
            .Where(row => row.IsIncluded)
            .ToDictionary(row => row.Id, row => row.AmountMinor, StringComparer.Ordinal);

        if (!ExpenseDetailsLogic.TotalMatchesFixed(ParticipantRows, _fixedTotalMinor))
        {
            StatusText = AppResources.Validation_TotalMustMatchAmount;
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

        IsEditMode = false;
        foreach (var row in ParticipantRows)
            row.IsEditMode = false;

        await LoadAsync();
    }

    public async Task ConfirmDeleteAsync()
    {
        if (IsArchived) return;
        await _dataService.DeleteExpenseAsync(_expenseId);
        ExpenseDeleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by code-behind when a participant raw-input entry fires TextChanged.</summary>
    public void OnParticipantRawInputChanged(ExpenseParticipantRowViewModel row, string text)
    {
        if (!IsEditMode) return;

        row.RawInput = text;
        if (ExpenseAmountParser.TryParseCommittedAmount(row.RawInput, out var minor))
        {
            row.AmountMinor = minor;
            row.IsEditing = false;
        }

        RebuildPreviewRows();
        RecalculateSaveState();
    }

    partial void OnExpenseTitleChanged(string value) => RecalculateSaveState();

    partial void OnSelectedPayerNameChanged(string? value)
    {
        if (!IsEditMode) return;

        foreach (var row in ParticipantRows)
            row.IsPayer = string.Equals(row.Name, value, StringComparison.Ordinal);

        RebuildPreviewRows();
        RecalculateSaveState();
    }

    private void BuildParticipantRows(ExpenseModel expense, string payerName)
    {
        ParticipantRows.Clear();
        var fixedComponent = expense.SplitDefinition.Components.OfType<FixedSplitComponent>().FirstOrDefault();
        var shares = fixedComponent?.Shares ?? new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var p in _participants)
        {
            if (!shares.TryGetValue(p.Id, out var amountMinor) || amountMinor <= 0) continue;

            ParticipantRows.Add(new ExpenseParticipantRowViewModel(
                p.Id, p.Name,
                string.Equals(p.Name, payerName, StringComparison.Ordinal),
                amountMinor, _currency));
        }
    }

    private void RebuildPreviewRows()
    {
        PreviewRows.Clear();
        var payerName = SelectedPayerName ?? string.Empty;
        foreach (var line in ExpenseDetailsLogic.BuildPreviewLines(ParticipantRows, payerName, _currency))
            PreviewRows.Add(new PreviewRowViewModel(line));
    }

    private void RecalculateSaveState()
    {
        var totalMatches = ExpenseDetailsLogic.TotalMatchesFixed(ParticipantRows, _fixedTotalMinor);
        StatusText = IsEditMode && !totalMatches
            ? AppResources.Validation_TotalMustMatchAmount
            : string.Empty;
        CanSave = ExpenseDetailsLogic.EvaluateSaveState(
            ParticipantRows, _fixedTotalMinor, IsEditMode, SelectedPayerName, ExpenseTitle);
    }
}
