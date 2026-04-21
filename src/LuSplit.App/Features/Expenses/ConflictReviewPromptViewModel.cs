using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Services;

namespace LuSplit.App.Features.Expenses;

/// <summary>
/// Prompt shown when an expense was changed remotely during offline use.
/// Shown by <see cref="ExpenseDetailsViewModel"/> when the conflict flag is set.
/// </summary>
public sealed partial class ConflictReviewPromptViewModel : ObservableObject
{
    private readonly ConflictFlagStore _store;
    private string? _entityId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool _hasConflict;

    public bool IsVisible => HasConflict;

    public string Message => "This expense was changed while you were offline. The latest version is shown.";

    public ConflictReviewPromptViewModel(ConflictFlagStore store)
    {
        _store = store;
    }

    public void Load(string entityId)
    {
        _entityId = entityId;
        HasConflict = _store.IsSet(entityId);
    }

    [RelayCommand]
    public void Dismiss()
    {
        if (_entityId is null) return;
        _store.Clear(_entityId);
        HasConflict = false;
    }
}
