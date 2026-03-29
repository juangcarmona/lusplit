using System.Globalization;

namespace LuSplit.App.Resources.Localization;

/// <summary>
/// Test stub for AppResources. Returns property key names as values — no RESX dependency.
/// </summary>
internal static class AppResources
{
    // Used by NoGroupsAvailableException
    public static string Startup_NoGroupsAvailable => nameof(Startup_NoGroupsAvailable);

    // Used by HomeViewModel
    public static string Home_AllSettled => nameof(Home_AllSettled);
    public static string Home_UnsettledFormat => nameof(Home_UnsettledFormat);

    // Used by TripPresentationMapper — event icon labels
    public static string AddEvent_IconOption_AnythingElse => nameof(AddEvent_IconOption_AnythingElse);
    public static string AddEvent_IconOption_Meal => nameof(AddEvent_IconOption_Meal);
    public static string AddEvent_IconOption_Transport => nameof(AddEvent_IconOption_Transport);
    public static string AddEvent_IconOption_Groceries => nameof(AddEvent_IconOption_Groceries);
    public static string AddEvent_IconOption_Tickets => nameof(AddEvent_IconOption_Tickets);
    public static string AddEvent_IconOption_Stay => nameof(AddEvent_IconOption_Stay);
    public static string AddEvent_IconOption_Drinks => nameof(AddEvent_IconOption_Drinks);
    public static string AddEvent_IconOption_Coffee => nameof(AddEvent_IconOption_Coffee);
    public static string AddEvent_IconOption_Fun => nameof(AddEvent_IconOption_Fun);

    // Used by TripPresentationMapper — mapper strings
    public static string Mapper_Person => "{0}";
    public static string Mapper_Everyone => nameof(Mapper_Everyone);
    public static string Mapper_And => "and";
    public static string Mapper_SplitEqual => nameof(Mapper_SplitEqual);
    public static string Mapper_SplitWeighted => nameof(Mapper_SplitWeighted);
    public static string Mapper_SplitByPercentage => nameof(Mapper_SplitByPercentage);
    public static string Mapper_SplitCustomAmounts => nameof(Mapper_SplitCustomAmounts);
    public static string Mapper_SplitCustomAmountsOnly => nameof(Mapper_SplitCustomAmountsOnly);
    public static string Mapper_HouseholdOf => "{0}";
    public static string Mapper_ResponsibilityOf => "{0}";
    public static string Mapper_PaymentTitle => nameof(Mapper_PaymentTitle);
    public static string Mapper_PaymentPrimaryText => "{0} → {1}";
    public static string Mapper_PaymentSecondaryText => nameof(Mapper_PaymentSecondaryText);
    public static string Mapper_ActivityExpenseTitle => "{0} {1}";
    public static string Mapper_ActivityPaymentTitle => "{0} → {1}";
    public static string Mapper_ActivityPaymentDetail => nameof(Mapper_ActivityPaymentDetail);
    public static string Mapper_BalanceOwes => "{0} owes {1}";
    public static string Mapper_BalanceEvenNow => nameof(Mapper_BalanceEvenNow);
    public static string Mapper_SummaryReadyToGo => nameof(Mapper_SummaryReadyToGo);
    public static string Mapper_SummaryEvents => "{0}";
    public static string Mapper_SummaryPeople => "{0} people";
    public static string Mapper_NoActivity => nameof(Mapper_NoActivity);
    public static string Mapper_Activity_Expense => "{0} expense";
    public static string Mapper_Activity_Expenses => "{0} expenses";
    public static string Mapper_Activity_Payment => "{0} payment";
    public static string Mapper_Activity_Payments => "{0} payments";
    public static string Mapper_Activity_ExpensesPayments => "{0} expenses, {1} payments";
    public static string Mapper_PeopleCountFormat => "{0}: {1}";
    public static string Mapper_Today => nameof(Mapper_Today);
    public static string Mapper_Yesterday => nameof(Mapper_Yesterday);
    public static string Mapper_Me => nameof(Mapper_Me);
    public static string Common_Unknown => nameof(Common_Unknown);

    // Used by RecordPaymentViewModel
    public static string Validation_ChooseBothPeople => nameof(Validation_ChooseBothPeople);
    public static string Validation_DifferentPeople => nameof(Validation_DifferentPeople);
    public static string Validation_InvalidAmount => nameof(Validation_InvalidAmount);
}
