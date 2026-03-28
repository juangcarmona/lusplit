using LuSplit.Application.Errors;

namespace LuSplit.Application.Commands;

internal static class UseCaseGuards
{
    internal static void AssertNonEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationError($"{fieldName} is required");
        }
    }

    internal static string ResolveDate(string? inputDate, string fallback)
    {
        var date = inputDate ?? fallback;
        if (!DateTimeOffset.TryParse(date, out _))
        {
            throw new ValidationError("date must be a valid ISO date");
        }

        return date;
    }
}
