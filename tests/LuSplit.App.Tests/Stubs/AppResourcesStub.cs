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

    // Used by GroupDetailsViewModel
    public static string Validation_GroupNameRequired => nameof(Validation_GroupNameRequired);
    public static string Validation_SelectCurrency => nameof(Validation_SelectCurrency);
    public static string Validation_GroupNotFound => nameof(Validation_GroupNotFound);
    public static string Export_Failed => "{0}";
    public static string Common_Cancel => nameof(Common_Cancel);
    public static string Common_Ok => nameof(Common_Ok);

    // Used by ParticipantDraftViewModel (stub doesn't need these but GroupDetailsModels.cs does)
    public static string GroupDetails_DependencyIndependent => nameof(GroupDetails_DependencyIndependent);
    public static string GroupDetails_DependencyDependsOnFormat => "{0}";
    public static string GroupDetails_DependencyResponsibleForFormat => "{0}";
    public static string GroupDetails_ConsumptionFull => nameof(GroupDetails_ConsumptionFull);
    public static string GroupDetails_ConsumptionHalf => nameof(GroupDetails_ConsumptionHalf);
    public static string GroupDetails_ConsumptionCustom => nameof(GroupDetails_ConsumptionCustom);

    // Used by CurrencyCatalog
    public static string Currency_DisplayFormat => "{0} {1} {2}";
    public static string Currency_Name_Unknown => nameof(Currency_Name_Unknown);
    public static string Currency_Name_EUR => nameof(Currency_Name_EUR);
    public static string Currency_Name_USD => nameof(Currency_Name_USD);
    public static string Currency_Name_GBP => nameof(Currency_Name_GBP);
    public static string Currency_Name_CHF => nameof(Currency_Name_CHF);
    public static string Currency_Name_JPY => nameof(Currency_Name_JPY);
    public static string Currency_Name_CNY => nameof(Currency_Name_CNY);
    public static string Currency_Name_INR => nameof(Currency_Name_INR);
    public static string Currency_Name_AUD => nameof(Currency_Name_AUD);
    public static string Currency_Name_CAD => nameof(Currency_Name_CAD);
    public static string Currency_Name_BRL => nameof(Currency_Name_BRL);
    public static string Currency_Name_MXN => nameof(Currency_Name_MXN);
    public static string Currency_Name_ARS => nameof(Currency_Name_ARS);
    public static string Currency_Name_CLP => nameof(Currency_Name_CLP);
    public static string Currency_Name_COP => nameof(Currency_Name_COP);
    public static string Currency_Name_PEN => nameof(Currency_Name_PEN);
    public static string Currency_Name_SEK => nameof(Currency_Name_SEK);
    public static string Currency_Name_NOK => nameof(Currency_Name_NOK);
    public static string Currency_Name_DKK => nameof(Currency_Name_DKK);
    public static string Currency_Name_PLN => nameof(Currency_Name_PLN);
    public static string Currency_Name_CZK => nameof(Currency_Name_CZK);
    public static string Currency_Name_HUF => nameof(Currency_Name_HUF);
    public static string Currency_Name_RON => nameof(Currency_Name_RON);
    public static string Currency_Name_TRY => nameof(Currency_Name_TRY);
    public static string Currency_Name_AED => nameof(Currency_Name_AED);
    public static string Currency_Name_SAR => nameof(Currency_Name_SAR);
    public static string Currency_Name_KRW => nameof(Currency_Name_KRW);
    public static string Currency_Name_SGD => nameof(Currency_Name_SGD);
    public static string Currency_Name_HKD => nameof(Currency_Name_HKD);
    public static string Currency_Name_NZD => nameof(Currency_Name_NZD);
    public static string Currency_Name_ZAR => nameof(Currency_Name_ZAR);

    // Used by CreateGroupViewModel
    public static string Validation_AddAtLeastOnePerson => nameof(Validation_AddAtLeastOnePerson);
    public static string Common_MeCapitalized => nameof(Common_MeCapitalized);

    // Used by GroupDetailsDependencyService / GroupDetailsPeopleService
    public static string Validation_PersonNameRequired => nameof(Validation_PersonNameRequired);
    public static string Validation_PersonNameMustBeUnique => nameof(Validation_PersonNameMustBeUnique);
    public static string Validation_ResponsiblePersonNotFound => nameof(Validation_ResponsiblePersonNotFound);
    public static string Validation_CircularDependency => nameof(Validation_CircularDependency);
}
