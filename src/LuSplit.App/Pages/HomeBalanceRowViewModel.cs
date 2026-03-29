namespace LuSplit.App.Pages;

/// <summary>
/// Row view model for a net-balance line on the Home and ArchivedGroupView pages.
/// </summary>
public sealed record HomeBalanceRowViewModel(
    string ParticipantId,
    string Name,
    string AmountText,
    bool IsPositive);
