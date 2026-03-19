using System.Collections.ObjectModel;
using System.Globalization;
using LuSplit.App.Resources.Localization;
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
public sealed record NetBalanceViewModel(string ParticipantId, string Name, string AmountText, bool IsPositive);
public sealed record SettlementSuggestionViewModel(string FromParticipantId, string ToParticipantId, string Text, string AmountText, long AmountMinor);
public sealed record CompactEventEntryViewModel(
    string Icon,
    string Title,
    string AmountText,
    string Subtitle,
    DateTimeOffset SortDate);
public sealed record EventIconOptionViewModel(string Icon, string Label)
{
    public string DisplayText => $"{Icon} {Label}";
}

public static class GroupPresentationMapper
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
                string.Format(AppResources.Mapper_ActivityExpenseTitle, ResolveParticipantName(expense.PaidByParticipantId, participantsById), expense.Title),
                DescribeExpense(expense, participantsById),
                FormatMinor(expense.AmountMinor, currency),
                ParseDate(expense.Date)))
            .Concat(overview.Transfers.Select(transfer => new ActivityEntryViewModel(
                "💸",
                string.Format(AppResources.Mapper_ActivityPaymentTitle, ResolveParticipantName(transfer.FromParticipantId, participantsById), ResolveParticipantName(transfer.ToParticipantId, participantsById)),
                AppResources.Mapper_ActivityPaymentDetail,
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
                string.Format(AppResources.Mapper_BalanceOwes, ResolveBalanceEntityName(transfer.FromParticipantId, overview, mode), ResolveBalanceEntityName(transfer.ToParticipantId, overview, mode)),
                FormatMinor(transfer.AmountMinor, overview.Group.Currency)))
            .ToArray();
    }

    public static IReadOnlyList<NetBalanceViewModel> BuildNetBalances(GroupOverviewModel overview)
    {
        var amountByParticipantId = overview.BalancesByParticipant
            .ToDictionary(balance => balance.EntityId, balance => balance.AmountMinor, StringComparer.Ordinal);

        return overview.BalancesByParticipant
            .Select(balance =>
            {
                var amountMinor = balance.AmountMinor;
                var isPositive = amountMinor > 0;
                var absAmountMinor = Math.Abs(amountMinor);
                var sign = isPositive ? "+" : amountMinor < 0 ? "-" : string.Empty;
                return new NetBalanceViewModel(
                    balance.EntityId,
                    ResolveParticipantName(balance.EntityId, overview),
                    $"{sign}{FormatMinor(absAmountMinor, overview.Group.Currency)}",
                    isPositive);
            })
            .OrderByDescending(line => amountByParticipantId.TryGetValue(line.ParticipantId, out var amount) ? amount : 0L)
            .ThenBy(line => line.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<SettlementSuggestionViewModel> BuildSettlementSuggestions(GroupOverviewModel overview)
    {
        var mode = ResolveSettlementMode(overview);
        var transfers = mode == SettlementMode.Participant
            ? overview.SettlementByParticipant.Transfers
            : overview.SettlementByEconomicUnitOwner.Transfers;

        return transfers
            .Select(transfer => new SettlementSuggestionViewModel(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                string.Create(
                    CultureInfo.CurrentCulture,
                    $"{ResolveBalanceEntityName(transfer.FromParticipantId, overview, mode)} → {ResolveBalanceEntityName(transfer.ToParticipantId, overview, mode)}"),
                FormatMinor(transfer.AmountMinor, overview.Group.Currency),
                transfer.AmountMinor))
            .ToArray();
    }

    public static IReadOnlyList<CompactEventEntryViewModel> BuildCompactEvents(
        GroupOverviewModel overview,
        IReadOnlyDictionary<string, string>? expenseIcons = null)
    {
        var participantsById = overview.Participants.ToDictionary(participant => participant.Id, participant => participant.Name, StringComparer.Ordinal);
        var currency = overview.Group.Currency;

        return overview.Expenses
            .Select(expense =>
            {
                var participantIds = expense.SplitDefinition.Components
                    .SelectMany(component => component switch
                    {
                        FixedSplitComponent fixedComponent => fixedComponent.Shares.Keys,
                        RemainderSplitComponent remainderComponent => remainderComponent.Participants,
                        _ => Array.Empty<string>()
                    })
                    .Distinct(StringComparer.Ordinal)
                    .Select(participantId => ResolveParticipantName(participantId, participantsById))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new CompactEventEntryViewModel(
                    ResolveExpenseIcon(expense, expenseIcons),
                    expense.Title,
                    FormatMinor(expense.AmountMinor, currency),
                    string.Create(CultureInfo.CurrentCulture, $"{ResolveParticipantName(expense.PaidByParticipantId, participantsById)} → {JoinNames(participantIds)}"),
                    ParseDate(expense.Date));
            })
            .Concat(overview.Transfers.Select(transfer => new CompactEventEntryViewModel(
                "💸",
                AppResources.Mapper_PaymentTitle,
                FormatMinor(transfer.AmountMinor, currency),
                string.Create(CultureInfo.CurrentCulture, $"{ResolveParticipantName(transfer.FromParticipantId, participantsById)} → {ResolveParticipantName(transfer.ToParticipantId, participantsById)}"),
                ParseDate(transfer.Date))))
            .OrderByDescending(item => item.SortDate)
            .ToArray();
    }

    public static string FormatCompactPeopleAndEvents(GroupOverviewModel overview)
        => string.Create(CultureInfo.CurrentCulture, $"{overview.Summary.ParticipantCount} people · {overview.Summary.ExpenseCount + overview.Summary.TransferCount} events");

    public static string FormatTotalUnsettled(GroupOverviewModel overview)
    {
        var totalUnsettledMinor = overview.BalancesByParticipant
            .Where(balance => balance.AmountMinor > 0)
            .Sum(balance => balance.AmountMinor);
        return FormatMinor(totalUnsettledMinor, overview.Group.Currency);
    }

    public static IReadOnlyList<string> BuildBalancePreview(GroupOverviewModel overview, int maxItems)
    {
        var lines = BuildWhoOwesWho(overview, ResolveSettlementMode(overview))
            .Take(maxItems)
            .Select(line => $"{line.Text} {line.AmountText}")
            .ToArray();

        return lines.Length == 0 ? new[] { AppResources.Mapper_BalanceEvenNow } : lines;
    }

    public static string BuildGroupSummary(GroupOverviewModel overview)
    {
        var eventCount = overview.Summary.ExpenseCount + overview.Summary.TransferCount;
        return eventCount == 0
            ? string.Format(AppResources.Mapper_SummaryReadyToGo, overview.Summary.ParticipantCount)
            : string.Format(AppResources.Mapper_SummaryEvents, overview.Summary.ParticipantCount, eventCount);
    }

    /// <summary>
    /// Picks the settlement mode that best reflects the group's participant structure.
    /// When all participants have their own economic unit (no dependents), participant-level
    /// and owner-level settlement are identical — either works. When some participants are
    /// dependents (share an economic unit with another participant), owner mode aggregates their
    /// balances under the responsible participant.
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
        // Use owner mode so the responsible participant surfaces as the settlement entity.
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
            string.Format(AppResources.Mapper_PaymentPrimaryText, ResolveParticipantName(expense.PaidByParticipantId, participantsById), FormatMinor(expense.AmountMinor, currency)),
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
            AppResources.Mapper_PaymentTitle,
            FormatMinor(transfer.AmountMinor, currency),
            string.Format(AppResources.Mapper_PaymentPrimaryText, ResolveParticipantName(transfer.FromParticipantId, participantsById), ResolveParticipantName(transfer.ToParticipantId, participantsById)),
            AppResources.Mapper_PaymentSecondaryText,
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
            return string.Format(AppResources.Mapper_SplitWeighted, peopleText);
        }

        if (expense.SplitDefinition.Components.OfType<RemainderSplitComponent>().Any(component => component.Mode == RemainderMode.Percent))
        {
            return string.Format(AppResources.Mapper_SplitByPercentage, peopleText);
        }

        if (expense.SplitDefinition.Components.OfType<FixedSplitComponent>().Any())
        {
            return expense.SplitDefinition.Components.OfType<RemainderSplitComponent>().Any()
                ? string.Format(AppResources.Mapper_SplitCustomAmounts, peopleText)
                : string.Format(AppResources.Mapper_SplitCustomAmountsOnly, peopleText);
        }

        return string.Format(AppResources.Mapper_SplitEqual, peopleText);
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

        // Only use the "X's Household" label when the unit actually has dependents.
        // A solo participant (no dependents) should appear by their own name.
        var hasDependents = overview.Participants.Any(p =>
            string.Equals(p.EconomicUnitId, unit.Id, StringComparison.Ordinal) &&
            !string.Equals(p.Id, entityId, StringComparison.Ordinal));

        var participantName = ResolveParticipantName(entityId, overview.Participants);
        return hasDependents
            ? string.Format(AppResources.Mapper_ResponsibilityOf, participantName)
            : participantName;
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
        => participants.FirstOrDefault(participant => string.Equals(participant.Id, participantId, StringComparison.Ordinal))?.Name ?? AppResources.Mapper_Person;

    private static string ResolveParticipantName(string participantId, IReadOnlyDictionary<string, string> participantsById)
        => participantsById.TryGetValue(participantId, out var name) ? name : AppResources.Mapper_Person;

    public static string DescribeDay(DateTimeOffset date)
    {
        var today = DateTimeOffset.Now.Date;
        var target = date.LocalDateTime.Date;

        if (target == today)
        {
            return AppResources.Mapper_Today;
        }

        if (target == today.AddDays(-1))
        {
            return AppResources.Mapper_Yesterday;
        }

        return date.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture);
    }

    public static DateTimeOffset ParseDate(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTimeOffset.MinValue;

    private static string JoinNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return AppResources.Mapper_Everyone;
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        var andWord = AppResources.Mapper_And;
        if (names.Count == 2)
        {
            return $"{names[0]} {andWord} {names[1]}";
        }

        return $"{string.Join(", ", names.Take(names.Count - 1))} {andWord} {names[^1]}";
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
            ? string.Create(CultureInfo.CurrentCulture, $"{amount:0.00} {currency.ToUpperInvariant()}")
            : string.Create(CultureInfo.CurrentCulture, $"{symbol}{amount:0.00}");
    }
}
