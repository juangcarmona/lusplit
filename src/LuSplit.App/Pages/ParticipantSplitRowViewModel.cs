using CommunityToolkit.Mvvm.ComponentModel;

namespace LuSplit.App.Pages;

/// <summary>Controls how a participant's share of an expense is determined on the add-expense page.</summary>
public enum SplitMode { Auto, Fixed, Percentage }

/// <summary>View-model for a single participant row on the add-expense split grid.</summary>
public sealed class ParticipantSplitRowViewModel : ObservableObject
{
    private bool _isIncluded;
    private SplitMode _splitMode = SplitMode.Auto;
    private decimal? _committedPercentage;
    private string _rawInput = string.Empty;
    private long _committedAmountMinor;
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
            if (_isIncluded == value) return;
            _isIncluded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIncludedMark));
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsViewing));
        }
    }

    public SplitMode SplitMode
    {
        get => _splitMode;
        set
        {
            if (_splitMode == value) return;
            _splitMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsViewing));
            OnPropertyChanged(nameof(ModeLabel));
        }
    }

    public decimal? CommittedPercentage
    {
        get => _committedPercentage;
        set
        {
            if (_committedPercentage == value) return;
            _committedPercentage = value;
            OnPropertyChanged();
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

    public long CommittedAmountMinor
    {
        get => _committedAmountMinor;
        set
        {
            var normalized = Math.Max(0, value);
            if (_committedAmountMinor == normalized) return;
            _committedAmountMinor = normalized;
            OnPropertyChanged();
        }
    }

    public string ValidationError
    {
        get => _validationError;
        set
        {
            if (string.Equals(_validationError, value, StringComparison.Ordinal)) return;
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
            if (_hasTransientInvalidInput == value) return;
            _hasTransientInvalidInput = value;
            OnPropertyChanged();
        }
    }

    public string DisplayValue
    {
        get => _displayValue;
        set
        {
            if (string.Equals(_displayValue, value, StringComparison.Ordinal)) return;
            _displayValue = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing => (_splitMode == SplitMode.Fixed || _splitMode == SplitMode.Percentage) && _isIncluded;
    public bool IsViewing => !IsEditing;
    public string IsIncludedMark => _isIncluded ? "✓" : " ";
    public bool HasValidationError => !string.IsNullOrWhiteSpace(_validationError);
    public string ModeLabel => _splitMode switch
    {
        SplitMode.Fixed => "💰▼",
        SplitMode.Percentage => "% ▼",
        _ => "⚖️▼"
    };

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
