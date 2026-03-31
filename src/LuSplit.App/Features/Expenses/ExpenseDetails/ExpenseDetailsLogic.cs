using LuSplit.App.Services.Formatting;

namespace LuSplit.App.Features.Expenses.ExpenseDetails;

/// <summary>
/// Pure static helpers for the expense-details edit flow.
/// No MAUI or AppResources dependencies — safe to test from a plain net10.0 project.
/// </summary>
internal static class ExpenseDetailsLogic
{
    /// <summary>
    /// Returns true when all conditions for saving an edited expense are met.
    /// </summary>
    public static bool EvaluateSaveState(
        IReadOnlyList<ExpenseParticipantRowViewModel> rows,
        long fixedTotalMinor,
        bool isEditMode,
        string? selectedPayerName,
        string expenseTitle)
    {
        if (!isEditMode) return false;

        var totalMinor = rows.Where(r => r.IsIncluded).Sum(r => r.AmountMinor);
        var hasIncluded = rows.Any(r => r.IsIncluded);
        var hasPayer = !string.IsNullOrWhiteSpace(selectedPayerName);
        var hasTitle = !string.IsNullOrWhiteSpace(expenseTitle);
        var totalMatches = totalMinor == fixedTotalMinor;

        return hasTitle && hasIncluded && hasPayer && totalMinor > 0 && totalMatches;
    }

    /// <summary>
    /// Returns true when the sum of included row amounts equals the fixed total.
    /// </summary>
    public static bool TotalMatchesFixed(
        IReadOnlyList<ExpenseParticipantRowViewModel> rows,
        long fixedTotalMinor)
    {
        var totalMinor = rows.Where(r => r.IsIncluded).Sum(r => r.AmountMinor);
        return totalMinor == fixedTotalMinor;
    }

    /// <summary>
    /// Builds settlement preview lines for the non-payer included participants.
    /// </summary>
    public static IReadOnlyList<string> BuildPreviewLines(
        IReadOnlyList<ExpenseParticipantRowViewModel> rows,
        string payerName,
        string currency)
    {
        return rows
            .Where(r => !r.IsPayer && r.IsIncluded && r.AmountMinor > 0)
            .Select(r => $"{r.Name} → {payerName} {CurrencyFormatter.FormatMinor(r.AmountMinor, currency)}")
            .ToList();
    }
}
