using CommunityToolkit.Mvvm.ComponentModel;
using LuSplit.App.Services.Formatting;

namespace LuSplit.App.Features.Expenses.ExpenseDetails;

public sealed class ExpenseParticipantRowViewModel : ObservableObject
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
            if (!_isIncluded) AmountMinor = 0;
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
    public string AmountText => CurrencyFormatter.FormatMinor(AmountMinor, _currency);
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
}

public sealed record PreviewRowViewModel(string Text);
