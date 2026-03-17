using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Services;

public sealed record TimelineEntryViewModel(
    string Icon,
    string Title,
    string AmountText,
    string PrimaryText,
    string SecondaryText,
    string DateText,
    DateTimeOffset SortDate);

public sealed record ActivityEntryViewModel(string Icon, string Title, string Detail, string AmountText, DateTimeOffset SortDate);

public sealed class ActivityDayGroupViewModel : ObservableCollection<ActivityEntryViewModel>
{
    public string Title { get; }

    public ActivityDayGroupViewModel(string title, IEnumerable<ActivityEntryViewModel> items)
        : base(items)
    {
        Title = title;
    }
}

public sealed record BalanceLineViewModel(string Text, string AmountText);
public sealed record EventIconOptionViewModel(string Icon, string Label)
{
    public string DisplayText => $"{Icon} {Label}";
}

public static class TripPresentationMapper
{
    private static readonly EventIconOptionViewModel[] BuiltInEventIcons =
    {
        new("✨", "Anything else"),
        new("🍝", "Meal"),
        new("🚕", "Transport"),
        new("🛒", "Groceries"),
        new("🎟", "Tickets"),
        new("🏨", "Stay"),
        new("🍻", "Drinks"),
        new("☕", "Coffee"),
        new("🎉", "Fun")
    };

    public static IReadOnlyList<EventIconOptionViewModel> GetEventIconOptions()
        => BuiltInEventIcons;

    public static EventIconOptionViewModel ResolveEventIconOption(string? icon)
        => BuiltInEventIcons.FirstOrDefault(option => string.Equals(option.Icon, icon, StringComparison.Ordinal))
            ?? BuiltInEventIcons[0];

    public static string SuggestEventIcon(string title)
        => IconForEvent(title);

    public static IReadOnlyList<TimelineEntryViewModel> BuildTimeline(GroupOverviewModel overview, IReadOnlyDictionary<string, string>? expenseIcons = null)
    {
        var participantsById = overview.Participants.ToDictionary(participant => participant.Id, participant => participant.Name, StringComparer.Ordinal);
        var currency = overview.Group.Currency;

        return overview.Expenses
            .Select(expense => BuildExpenseTimelineEntry(expense, participantsById, currency, expenseIcons))
            .Concat(overview.Transfers.Select(transfer => BuildPaymentTimelineEntry(transfer, participantsById, currency)))
            .OrderByDescending(item => item.SortDate)
            .ThenByDescending(item => item.Title, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ActivityDayGroupViewModel> BuildActivity(GroupOverviewModel overview, IReadOnlyDictionary<string, string>? expenseIcons = null)
    {
        var participantsById = overview.Participants.ToDictionary(participant => participant.Id, participant => participant.Name, StringComparer.Ordinal);
        var currency = overview.Group.Currency;

        var items = overview.Expenses
            .Select(expense => new ActivityEntryViewModel(
                ResolveExpenseIcon(expense, expenseIcons),
                $"{ResolveParticipantName(expense.PaidByParticipantId, participantsById)} added {expense.Title}",
                DescribeExpense(expense, participantsById),
                FormatMinor(expense.AmountMinor, currency),
                ParseDate(expense.Date)))
            .Concat(overview.Transfers.Select(transfer => new ActivityEntryViewModel(
                "💸",
                $"{ResolveParticipantName(transfer.FromParticipantId, participantsById)} paid {ResolveParticipantName(transfer.ToParticipantId, participantsById)}",
                "Payment recorded",
                FormatMinor(transfer.AmountMinor, currency),
                ParseDate(transfer.Date))))
            .OrderByDescending(item => item.SortDate)
            .ToArray();

        return items
            .GroupBy(item => DescribeDay(item.SortDate))
            .Select(group => new ActivityDayGroupViewModel(group.Key, group))
            .ToArray();
    }

    public static IReadOnlyList<BalanceLineViewModel> BuildWhoOwesWho(GroupOverviewModel overview, SettlementMode mode)
    {
        var transfers = mode == SettlementMode.Participant
            ? overview.SettlementByParticipant.Transfers
            : overview.SettlementByEconomicUnitOwner.Transfers;

        return transfers
            .Select(transfer => new BalanceLineViewModel(
                $"{ResolveBalanceEntityName(transfer.FromParticipantId, overview, mode)} owes {ResolveBalanceEntityName(transfer.ToParticipantId, overview, mode)}",
                FormatMinor(transfer.AmountMinor, overview.Group.Currency)))
            .ToArray();
    }

    public static IReadOnlyList<string> BuildBalancePreview(GroupOverviewModel overview, int maxItems)
    {
        var lines = BuildWhoOwesWho(overview, SettlementMode.Participant)
            .Take(maxItems)
            .Select(line => $"{line.Text} {line.AmountText}")
            .ToArray();

        return lines.Length == 0 ? new[] { "Everyone is even right now." } : lines;
    }

    public static string BuildTripSummary(GroupOverviewModel overview)
    {
        var eventCount = overview.Summary.ExpenseCount + overview.Summary.TransferCount;
        return eventCount == 0
            ? $"{overview.Summary.ParticipantCount} people ready to go"
            : $"{overview.Summary.ParticipantCount} people • {eventCount} events";
    }

    /// <summary>
    /// Picks the settlement mode that best reflects the trip's participant structure.
    /// When all participants have their own economic unit (no dependents), participant-level
    /// and owner-level settlement are identical — either works. When some participants are
    /// dependents (share an economic unit with another payer), owner mode aggregates their
    /// balances under the payer, which is the natural family / household view.
    /// </summary>
    public static SettlementMode ResolveSettlementMode(GroupOverviewModel overview)
    {
        // If the number of economic units equals the number of participants, every person
        // has their own independent unit — both modes produce the same result.
        if (overview.EconomicUnits.Count >= overview.Participants.Count)
        {
            return SettlementMode.Participant;
        }

        // At least one economic unit has multiple participants (dependents exist).
        // Use owner mode so the household payer surfaces as the settlement entity.
        return SettlementMode.EconomicUnitOwner;
    }

    private static TimelineEntryViewModel BuildExpenseTimelineEntry(
        ExpenseModel expense,
        IReadOnlyDictionary<string, string> participantsById,
        string currency,
        IReadOnlyDictionary<string, string>? expenseIcons)
    {
        var date = ParseDate(expense.Date);
        return new TimelineEntryViewModel(
            ResolveExpenseIcon(expense, expenseIcons),
            expense.Title,
            FormatMinor(expense.AmountMinor, currency),
            $"{ResolveParticipantName(expense.PaidByParticipantId, participantsById)} paid {FormatMinor(expense.AmountMinor, currency)}",
            DescribeExpense(expense, participantsById),
            DescribeDay(date),
            date);
    }

    private static TimelineEntryViewModel BuildPaymentTimelineEntry(
        TransferModel transfer,
        IReadOnlyDictionary<string, string> participantsById,
        string currency)
    {
        var date = ParseDate(transfer.Date);
        return new TimelineEntryViewModel(
            "💸",
            "Payment",
            FormatMinor(transfer.AmountMinor, currency),
            $"{ResolveParticipantName(transfer.FromParticipantId, participantsById)} paid {ResolveParticipantName(transfer.ToParticipantId, participantsById)}",
            "Recorded payment",
            DescribeDay(date),
            date);
    }

    private static string DescribeExpense(ExpenseModel expense, IReadOnlyDictionary<string, string> participantsById)
    {
        var participantIds = expense.SplitDefinition.Components
            .SelectMany(component => component switch
            {
                FixedSplitComponent fixedComponent => fixedComponent.Shares.Keys,
                RemainderSplitComponent remainderComponent => remainderComponent.Participants,
                _ => Array.Empty<string>()
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(participantId => ResolveParticipantName(participantId, participantsById), StringComparer.Ordinal)
            .ToArray();

        var people = participantIds.Select(participantId => ResolveParticipantName(participantId, participantsById)).ToArray();
        var peopleText = JoinNames(people);

        if (expense.SplitDefinition.Components.OfType<RemainderSplitComponent>().Any(component => component.Mode == RemainderMode.Weight))
        {
            return $"Weighted between {peopleText}";
        }

        if (expense.SplitDefinition.Components.OfType<RemainderSplitComponent>().Any(component => component.Mode == RemainderMode.Percent))
        {
            return $"Shared by percentages between {peopleText}";
        }

        if (expense.SplitDefinition.Components.OfType<FixedSplitComponent>().Any())
        {
            return expense.SplitDefinition.Components.OfType<RemainderSplitComponent>().Any()
                ? $"Shared with custom amounts between {peopleText}"
                : $"Custom amounts for {peopleText}";
        }

        return $"Shared equally between {peopleText}";
    }

    private static string ResolveExpenseIcon(ExpenseModel expense, IReadOnlyDictionary<string, string>? expenseIcons)
    {
        if (expenseIcons is not null && expenseIcons.TryGetValue(expense.Id, out var explicitIcon) && !string.IsNullOrWhiteSpace(explicitIcon))
        {
            return explicitIcon;
        }

        return IconForEvent(expense.Title);
    }

    private static string ResolveBalanceEntityName(string entityId, GroupOverviewModel overview, SettlementMode mode)
    {
        if (mode == SettlementMode.Participant)
        {
            return ResolveParticipantName(entityId, overview.Participants);
        }

        var unit = overview.EconomicUnits.FirstOrDefault(candidate => string.Equals(candidate.OwnerParticipantId, entityId, StringComparison.Ordinal));
        if (unit is null)
        {
            return ResolveParticipantName(entityId, overview.Participants);
        }

        if (!string.IsNullOrWhiteSpace(unit.Name))
        {
            return unit.Name!;
        }

        return $"{ResolveParticipantName(entityId, overview.Participants)}'s household";
    }

    private static string IconForEvent(string title)
    {
        var normalized = title.Trim().ToLowerInvariant();

        if (normalized.Contains("dinner", StringComparison.Ordinal)
            || normalized.Contains("lunch", StringComparison.Ordinal)
            || normalized.Contains("breakfast", StringComparison.Ordinal)
            || normalized.Contains("food", StringComparison.Ordinal))
        {
            return "🍝";
        }

        if (normalized.Contains("taxi", StringComparison.Ordinal)
            || normalized.Contains("ride", StringComparison.Ordinal)
            || normalized.Contains("bus", StringComparison.Ordinal)
            || normalized.Contains("train", StringComparison.Ordinal)
            || normalized.Contains("transport", StringComparison.Ordinal))
        {
            return "🚕";
        }

        if (normalized.Contains("hotel", StringComparison.Ordinal)
            || normalized.Contains("lodging", StringComparison.Ordinal)
            || normalized.Contains("stay", StringComparison.Ordinal))
        {
            return "🏨";
        }

        if (normalized.Contains("ticket", StringComparison.Ordinal)
            || normalized.Contains("pass", StringComparison.Ordinal))
        {
            return "🎟";
        }

        if (normalized.Contains("grocery", StringComparison.Ordinal)
            || normalized.Contains("groceries", StringComparison.Ordinal)
            || normalized.Contains("store", StringComparison.Ordinal))
        {
            return "🛒";
        }

        if (normalized.Contains("drink", StringComparison.Ordinal)
            || normalized.Contains("beer", StringComparison.Ordinal)
            || normalized.Contains("bar", StringComparison.Ordinal))
        {
            return "🍻";
        }

        return "✨";
    }

    private static string ResolveParticipantName(string participantId, GroupOverviewModel overview)
        => ResolveParticipantName(participantId, overview.Participants);

    private static string ResolveParticipantName(string participantId, IReadOnlyList<ParticipantModel> participants)
        => participants.FirstOrDefault(participant => string.Equals(participant.Id, participantId, StringComparison.Ordinal))?.Name ?? "Person";

    private static string ResolveParticipantName(string participantId, IReadOnlyDictionary<string, string> participantsById)
        => participantsById.TryGetValue(participantId, out var name) ? name : "Person";

    public static string DescribeDay(DateTimeOffset date)
    {
        var today = DateTimeOffset.Now.Date;
        var target = date.LocalDateTime.Date;

        if (target == today)
        {
            return "Today";
        }

        if (target == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return date.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseDate(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTimeOffset.MinValue;

    private static string JoinNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return "everyone";
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        if (names.Count == 2)
        {
            return $"{names[0]} and {names[1]}";
        }

        return $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
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
            ? string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency.ToUpperInvariant()}")
            : string.Create(CultureInfo.InvariantCulture, $"{symbol}{amount:0.00}");
    }
}
