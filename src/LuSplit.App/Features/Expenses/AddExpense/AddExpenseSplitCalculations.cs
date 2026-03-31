namespace LuSplit.App.Features.Expenses.AddExpense;

/// <summary>
/// Pure static helpers for computing participant split shares on the add-expense page.
/// Contains no MAUI or platform dependencies — safe to test from a plain net10.0 project.
/// </summary>
internal static class AddExpenseSplitCalculations
{
    /// <summary>
    /// Validates the form state and assigns CommittedAmountMinor to all participant rows.
    /// Returns an empty string on success, or a user-facing validation message on failure.
    /// </summary>
    public static string ComputeRows(
        IReadOnlyList<ParticipantSplitRowViewModel> rows,
        string amountText,
        string invalidAmountMessage,
        string atLeastOnePersonMessage)
    {
        if (!ExpenseAmountParser.TryParseAmountLenient(amountText, out var totalMinor))
        {
            ResetAllShares(rows);
            return invalidAmountMessage;
        }

        var included = rows.Where(row => row.IsIncluded).ToArray();
        if (included.Length < 2)
        {
            ResetAllShares(rows);
            return atLeastOnePersonMessage;
        }

        if (included.Any(row => row.HasTransientInvalidInput))
        {
            return invalidAmountMessage;
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
            return invalidAmountMessage;
        }

        if (autoRows.Length == 0)
        {
            if (remaining != 0)
            {
                return invalidAmountMessage;
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

        foreach (var row in rows.Where(row => !row.IsIncluded))
        {
            row.CommittedAmountMinor = 0;
        }

        return string.Empty;
    }

    public static void ResetAllShares(IReadOnlyList<ParticipantSplitRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            row.CommittedAmountMinor = 0;
        }
    }
}
