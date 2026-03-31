using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Formatting;
using LuSplit.App.Services.Settings;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Payments.Models;
using LuSplit.Domain.Expenses;
using System.Collections.ObjectModel;
using System.Globalization;

namespace LuSplit.App.Services.Presentation;

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
    string SourceId,
    bool IsExpense,
    string Icon,
    string Line1,
    string Line2,
    string SortTieBreaker,
    DateTimeOffset SortDate);
public sealed record EventIconOptionViewModel(string Icon, string Label)
{
    public string DisplayText => $"{Icon} {Label}";
}

public static class GroupPresentationMapper
{
    private static readonly (string Icon, Func<string> LabelFactory)[] BuiltInEventIconDefinitions =
    {
        ("✨", () => AppResources.AddEvent_IconOption_AnythingElse),
        ("🍝", () => AppResources.AddEvent_IconOption_Meal),
        ("🚕", () => AppResources.AddEvent_IconOption_Transport),
        ("🛒", () => AppResources.AddEvent_IconOption_Groceries),
        ("🎟", () => AppResources.AddEvent_IconOption_Tickets),
        ("🏨", () => AppResources.AddEvent_IconOption_Stay),
        ("🍻", () => AppResources.AddEvent_IconOption_Drinks),
        ("☕", () => AppResources.AddEvent_IconOption_Coffee),
        ("🎉", () => AppResources.AddEvent_IconOption_Fun)
    };

    public static IReadOnlyList<EventIconOptionViewModel> GetEventIconOptions()
    {
        var options = new EventIconOptionViewModel[BuiltInEventIconDefinitions.Length];
        for (var i = 0; i < BuiltInEventIconDefinitions.Length; i++)
        {
            var descriptor = BuiltInEventIconDefinitions[i];
            options[i] = new EventIconOptionViewModel(descriptor.Icon, descriptor.LabelFactory());
        }

        return options;
    }

    public static EventIconOptionViewModel ResolveEventIconOption(string? icon)
    {
        for (var i = 0; i < BuiltInEventIconDefinitions.Length; i++)
        {
            var definition = BuiltInEventIconDefinitions[i];
            if (string.Equals(definition.Icon, icon, StringComparison.Ordinal))
            {
                return new EventIconOptionViewModel(definition.Icon, definition.LabelFactory());
            }
        }

        var fallback = BuiltInEventIconDefinitions[0];
        return new EventIconOptionViewModel(fallback.Icon, fallback.LabelFactory());
    }

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
                CurrencyFormatter.FormatMinor(expense.AmountMinor, currency),
                ParseDate(expense.Date)))
            .Concat(overview.Transfers.Select(transfer => new ActivityEntryViewModel(
                "💸",
                string.Format(AppResources.Mapper_ActivityPaymentTitle, ResolveParticipantName(transfer.FromParticipantId, participantsById), ResolveParticipantName(transfer.ToParticipantId, participantsById)),
                AppResources.Mapper_ActivityPaymentDetail,
                CurrencyFormatter.FormatMinor(transfer.AmountMinor, currency),
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
                CurrencyFormatter.FormatMinor(transfer.AmountMinor, overview.Group.Currency)))
            .ToArray();
    }

    public static IReadOnlyList<NetBalanceViewModel> BuildNetBalances(GroupOverviewModel overview, SettlementMode mode)
    {
        var balances = mode == SettlementMode.Participant
            ? overview.BalancesByParticipant
            : overview.BalancesByEconomicUnitOwner;

        var amountByParticipantId = balances
            .ToDictionary(balance => balance.EntityId, balance => balance.AmountMinor, StringComparer.Ordinal);

        return balances
            .Where(balance => balance.AmountMinor != 0)
            .Select(balance =>
            {
                var amountMinor = balance.AmountMinor;
                var isPositive = amountMinor > 0;
                var absAmountMinor = Math.Abs(amountMinor);
                var sign = isPositive ? "+" : amountMinor < 0 ? "-" : string.Empty;
                return new NetBalanceViewModel(
                    balance.EntityId,
                    ResolveParticipantName(balance.EntityId, overview),
                    $"{sign}{CurrencyFormatter.FormatMinor(absAmountMinor, overview.Group.Currency)}",
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
                CurrencyFormatter.FormatMinor(transfer.AmountMinor, overview.Group.Currency),
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
                    expense.Id,
                    true,
                    ResolveExpenseIcon(expense, expenseIcons),
                    string.Create(CultureInfo.CurrentCulture, $"{expense.Title} - {CurrencyFormatter.FormatMinor(expense.AmountMinor, currency)}"),
                    string.Format(
                        AppResources.Mapper_PeopleCountFormat,
                        ResolveParticipantName(expense.PaidByParticipantId, participantsById),
                        participantIds.Length),
                    expense.Id,
                    ParseDate(expense.Date));
            })
            .Concat(overview.Transfers.Select(transfer => new CompactEventEntryViewModel(
                transfer.Id,
                false,
                "💸",
                string.Create(CultureInfo.CurrentCulture, $"{AppResources.Mapper_PaymentTitle} - {CurrencyFormatter.FormatMinor(transfer.AmountMinor, currency)}"),
                string.Create(CultureInfo.CurrentCulture, $"{ResolveParticipantName(transfer.FromParticipantId, participantsById)} → {ResolveParticipantName(transfer.ToParticipantId, participantsById)}"),
                transfer.Id,
                ParseDate(transfer.Date))))
            .OrderByDescending(item => item.SortDate)
            .ThenByDescending(item => item.SortTieBreaker, StringComparer.Ordinal)
            .ToArray();
    }

    public static string FormatCompactPeopleAndEvents(GroupOverviewModel overview)
        => FormatActivitySummary(
            overview.Summary.ParticipantCount,
            overview.Summary.ExpenseCount,
            overview.Summary.TransferCount);

    public static string FormatTotalUnsettled(GroupOverviewModel overview)
    {
        var totalUnsettledMinor = overview.BalancesByParticipant
            .Where(balance => balance.AmountMinor > 0)
            .Sum(balance => balance.AmountMinor);
        return CurrencyFormatter.FormatMinor(totalUnsettledMinor, overview.Group.Currency);
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
        => FormatActivitySummary(
            overview.Summary.ParticipantCount,
            overview.Summary.ExpenseCount,
            overview.Summary.TransferCount);

    private static string FormatActivitySummary(int people, int expenses, int transfers)
    {
        var peoplePart = string.Format(AppResources.Mapper_SummaryPeople, people);
        var activityPart = (expenses, transfers) switch
        {
            (0, 0) => AppResources.Mapper_NoActivity,
            (1, 0) => string.Format(AppResources.Mapper_Activity_Expense, expenses),
            (_, 0) => string.Format(AppResources.Mapper_Activity_Expenses, expenses),
            (0, 1) => string.Format(AppResources.Mapper_Activity_Payment, transfers),
            (0, _) => string.Format(AppResources.Mapper_Activity_Payments, transfers),
            _      => string.Format(AppResources.Mapper_Activity_ExpensesPayments, expenses, transfers)
        };
        return $"{peoplePart} · {activityPart}";
    }

    /// <summary>
    /// Picks the settlement mode that best reflects the group's participant structure.
    /// When all participants have their own economic unit (no dependents), participant-level
    /// and owner-level settlement are identical - either works. When some participants are
    /// dependents (share an economic unit with another participant), owner mode aggregates their
    /// balances under the responsible participant.
    /// </summary>
    public static SettlementMode ResolveSettlementMode(GroupOverviewModel overview)
    {
        // If the number of economic units equals the number of participants, every person
        // has their own independent unit - both modes produce the same result.
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
            CurrencyFormatter.FormatMinor(expense.AmountMinor, currency),
            string.Format(AppResources.Mapper_PaymentPrimaryText, ResolveParticipantName(expense.PaidByParticipantId, participantsById), CurrencyFormatter.FormatMinor(expense.AmountMinor, currency)),
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
            CurrencyFormatter.FormatMinor(transfer.AmountMinor, currency),
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
        => AnnotateIfCurrentUser(participants.FirstOrDefault(participant => string.Equals(participant.Id, participantId, StringComparison.Ordinal))?.Name ?? AppResources.Mapper_Person);

    private static string ResolveParticipantName(string participantId, IReadOnlyDictionary<string, string> participantsById)
        => AnnotateIfCurrentUser(participantsById.TryGetValue(participantId, out var name) ? name : AppResources.Mapper_Person);

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

    private static string AnnotateIfCurrentUser(string name)
        => UserProfilePreferences.AnnotateIfCurrentUser(name);
}
