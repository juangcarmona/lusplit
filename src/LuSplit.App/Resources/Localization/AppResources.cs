using System.Globalization;
using System.Resources;

namespace LuSplit.App.Resources.Localization;

/// <summary>
/// Strongly-typed accessor for AppResources.resx, supporting runtime culture switching via x:Static.
/// </summary>
public static class AppResources
{
    private static readonly ResourceManager _rm =
        new("LuSplit.App.Resources.Localization.AppResources", typeof(AppResources).Assembly);

    private static string Get(string key) =>
        _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // --- Activity ---
    public static string Activity_Title => Get(nameof(Activity_Title));
    public static string Activity_Subtitle => Get(nameof(Activity_Subtitle));
    public static string Activity_EmptyTitle => Get(nameof(Activity_EmptyTitle));
    public static string Activity_EmptySubtitle => Get(nameof(Activity_EmptySubtitle));

    // --- Groups (Home) ---
    public static string Groups_Title => Get(nameof(Groups_Title));
    public static string Groups_Subtitle => Get(nameof(Groups_Subtitle));
    public static string Groups_EmptyTitle => Get(nameof(Groups_EmptyTitle));
    public static string Groups_EmptySubtitle => Get(nameof(Groups_EmptySubtitle));
    public static string Groups_Open => Get(nameof(Groups_Open));
    public static string Groups_Details => Get(nameof(Groups_Details));
    public static string Groups_NewGroup => Get(nameof(Groups_NewGroup));
    public static string Groups_Active => Get(nameof(Groups_Active));
    public static string Groups_ViewArchived => Get(nameof(Groups_ViewArchived));

    // --- Group timeline ---
    public static string Group_Title => Get(nameof(Group_Title));
    public static string Group_DetailsButton => Get(nameof(Group_DetailsButton));
    public static string Group_WhoOwesWhat => Get(nameof(Group_WhoOwesWhat));
    public static string Group_EveryoneEven => Get(nameof(Group_EveryoneEven));
    public static string Group_SettleUp => Get(nameof(Group_SettleUp));
    public static string Group_Events => Get(nameof(Group_Events));
    public static string Group_EmptyTitle => Get(nameof(Group_EmptyTitle));
    public static string Group_EmptySubtitle => Get(nameof(Group_EmptySubtitle));
    public static string Group_AddExpense => Get(nameof(Group_AddExpense));
    public static string Group_RecordPayment => Get(nameof(Group_RecordPayment));

    // --- Group Details ---
    public static string GroupDetails_Title => Get(nameof(GroupDetails_Title));
    public static string GroupDetails_PageTitleCreate => Get(nameof(GroupDetails_PageTitleCreate));
    public static string GroupDetails_PageTitleEdit => Get(nameof(GroupDetails_PageTitleEdit));
    public static string GroupDetails_SubtitleCreate => Get(nameof(GroupDetails_SubtitleCreate));
    public static string GroupDetails_SubtitleEdit => Get(nameof(GroupDetails_SubtitleEdit));
    public static string GroupDetails_GroupNameLabel => Get(nameof(GroupDetails_GroupNameLabel));
    public static string GroupDetails_GroupNamePlaceholder => Get(nameof(GroupDetails_GroupNamePlaceholder));
    public static string GroupDetails_CurrencyLabel => Get(nameof(GroupDetails_CurrencyLabel));
    public static string GroupDetails_PeopleSection => Get(nameof(GroupDetails_PeopleSection));
    public static string GroupDetails_HouseholdHint => Get(nameof(GroupDetails_HouseholdHint));
    public static string GroupDetails_NoPeopleTitle => Get(nameof(GroupDetails_NoPeopleTitle));
    public static string GroupDetails_NoPeopleSubtitle => Get(nameof(GroupDetails_NoPeopleSubtitle));
    public static string GroupDetails_AddPersonSection => Get(nameof(GroupDetails_AddPersonSection));
    public static string GroupDetails_PersonNamePlaceholder => Get(nameof(GroupDetails_PersonNamePlaceholder));
    public static string GroupDetails_HouseholdNamePlaceholder => Get(nameof(GroupDetails_HouseholdNamePlaceholder));
    public static string GroupDetails_AddPersonButton => Get(nameof(GroupDetails_AddPersonButton));
    public static string GroupDetails_CreateButton => Get(nameof(GroupDetails_CreateButton));
    public static string GroupDetails_SaveButton => Get(nameof(GroupDetails_SaveButton));
    public static string GroupDetails_ExportButton => Get(nameof(GroupDetails_ExportButton));
    public static string GroupDetails_SettlesOnOwn => Get(nameof(GroupDetails_SettlesOnOwn));
    public static string GroupDetails_PersonAdded => Get(nameof(GroupDetails_PersonAdded));
    public static string GroupDetails_PersonAddedNew => Get(nameof(GroupDetails_PersonAddedNew));
    public static string GroupDetails_Remove => Get(nameof(GroupDetails_Remove));
    public static string GroupDetails_ArchiveButton => Get(nameof(GroupDetails_ArchiveButton));
    public static string GroupDetails_ArchiveConfirmTitle => Get(nameof(GroupDetails_ArchiveConfirmTitle));
    public static string GroupDetails_ArchiveConfirmMessage => Get(nameof(GroupDetails_ArchiveConfirmMessage));
    public static string GroupDetails_ArchiveConfirmYes => Get(nameof(GroupDetails_ArchiveConfirmYes));
    public static string GroupDetails_ArchivedStatus => Get(nameof(GroupDetails_ArchivedStatus));
    public static string GroupDetails_ConsumptionCategoryLabel => Get(nameof(GroupDetails_ConsumptionCategoryLabel));
    public static string GroupDetails_ConsumptionFull => Get(nameof(GroupDetails_ConsumptionFull));
    public static string GroupDetails_ConsumptionHalf => Get(nameof(GroupDetails_ConsumptionHalf));
    public static string GroupDetails_ConsumptionCustom => Get(nameof(GroupDetails_ConsumptionCustom));
    public static string GroupDetails_CustomWeightPlaceholder => Get(nameof(GroupDetails_CustomWeightPlaceholder));
    public static string GroupDetails_PersonIsOwner => Get(nameof(GroupDetails_PersonIsOwner));
    public static string GroupDetails_PersonIsDependent => Get(nameof(GroupDetails_PersonIsDependent));
    public static string GroupDetails_AddPersonHint => Get(nameof(GroupDetails_AddPersonHint));

    // --- Add Event ---
    public static string AddEvent_Title => Get(nameof(AddEvent_Title));
    public static string AddEvent_Subtitle => Get(nameof(AddEvent_Subtitle));
    public static string AddEvent_WhatHappened => Get(nameof(AddEvent_WhatHappened));
    public static string AddEvent_QuickDinner => Get(nameof(AddEvent_QuickDinner));
    public static string AddEvent_QuickTaxi => Get(nameof(AddEvent_QuickTaxi));
    public static string AddEvent_QuickGroceries => Get(nameof(AddEvent_QuickGroceries));
    public static string AddEvent_QuickTickets => Get(nameof(AddEvent_QuickTickets));
    public static string AddEvent_QuickCustom => Get(nameof(AddEvent_QuickCustom));
    public static string AddEvent_EventLabel => Get(nameof(AddEvent_EventLabel));
    public static string AddEvent_EventPlaceholder => Get(nameof(AddEvent_EventPlaceholder));
    public static string AddEvent_PaidBy => Get(nameof(AddEvent_PaidBy));
    public static string AddEvent_WhoJoined => Get(nameof(AddEvent_WhoJoined));
    public static string AddEvent_EveryoneDefault => Get(nameof(AddEvent_EveryoneDefault));
    public static string AddEvent_SaveButton => Get(nameof(AddEvent_SaveButton));
    public static string AddEvent_Saved => Get(nameof(AddEvent_Saved));

    // --- Settlement ---
    public static string Settlement_Title => Get(nameof(Settlement_Title));
    public static string Settlement_WhoOwesWhat => Get(nameof(Settlement_WhoOwesWhat));
    public static string Settlement_Subtitle => Get(nameof(Settlement_Subtitle));
    public static string Settlement_EmptyTitle => Get(nameof(Settlement_EmptyTitle));
    public static string Settlement_EmptySubtitle => Get(nameof(Settlement_EmptySubtitle));
    public static string Settlement_RecordPaymentButton => Get(nameof(Settlement_RecordPaymentButton));

    // --- Record Payment ---
    public static string RecordPayment_Title => Get(nameof(RecordPayment_Title));
    public static string RecordPayment_Subtitle => Get(nameof(RecordPayment_Subtitle));
    public static string RecordPayment_WhoPaid => Get(nameof(RecordPayment_WhoPaid));
    public static string RecordPayment_WhoReceived => Get(nameof(RecordPayment_WhoReceived));
    public static string RecordPayment_SaveButton => Get(nameof(RecordPayment_SaveButton));

    // --- Common ---
    public static string Common_Amount => Get(nameof(Common_Amount));
    public static string Common_Date => Get(nameof(Common_Date));
    public static string Common_Cancel => Get(nameof(Common_Cancel));

    // --- Export ---
    public static string Export_DialogTitle => Get(nameof(Export_DialogTitle));
    public static string Export_JsonOption => Get(nameof(Export_JsonOption));
    public static string Export_CsvOption => Get(nameof(Export_CsvOption));
    public static string Export_PdfOption => Get(nameof(Export_PdfOption));
    public static string Export_ShareTitle => Get(nameof(Export_ShareTitle));
    public static string Export_Failed => Get(nameof(Export_Failed));

    // --- Validation ---
    public static string Validation_TitleRequired => Get(nameof(Validation_TitleRequired));
    public static string Validation_InvalidAmount => Get(nameof(Validation_InvalidAmount));
    public static string Validation_SelectPayer => Get(nameof(Validation_SelectPayer));
    public static string Validation_PickAtLeastOnePerson => Get(nameof(Validation_PickAtLeastOnePerson));
    public static string Validation_PersonNameRequired => Get(nameof(Validation_PersonNameRequired));
    public static string Validation_GroupNotFound => Get(nameof(Validation_GroupNotFound));
    public static string Validation_GroupNameRequired => Get(nameof(Validation_GroupNameRequired));
    public static string Validation_SelectCurrency => Get(nameof(Validation_SelectCurrency));
    public static string Validation_AddAtLeastOnePerson => Get(nameof(Validation_AddAtLeastOnePerson));
    public static string Validation_ChooseBothPeople => Get(nameof(Validation_ChooseBothPeople));
    public static string Validation_DifferentPeople => Get(nameof(Validation_DifferentPeople));
    public static string Validation_InvalidCustomWeight => Get(nameof(Validation_InvalidCustomWeight));
    public static string Validation_CustomWeightRequiredForCustomCategory => Get(nameof(Validation_CustomWeightRequiredForCustomCategory));

    // --- Archived groups ---
    public static string Archived_Title => Get(nameof(Archived_Title));
    public static string Archived_Subtitle => Get(nameof(Archived_Subtitle));
    public static string Archived_EmptyTitle => Get(nameof(Archived_EmptyTitle));
    public static string Archived_EmptySubtitle => Get(nameof(Archived_EmptySubtitle));
    public static string Archived_Badge => Get(nameof(Archived_Badge));

    // --- Settings ---
    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string Settings_LanguageSection => Get(nameof(Settings_LanguageSection));
    public static string Settings_LanguageHint => Get(nameof(Settings_LanguageHint));
    public static string Settings_LanguageSaved => Get(nameof(Settings_LanguageSaved));
    public static string Language_SystemDefault => Get(nameof(Language_SystemDefault));

    // --- Validation (service-level) ---
    public static string Validation_CurrencyRequired => Get(nameof(Validation_CurrencyRequired));
    public static string Validation_EachPersonNeedsName => Get(nameof(Validation_EachPersonNeedsName));

    // --- Mapper / presentation sentences ---
    public static string Mapper_Person => Get(nameof(Mapper_Person));
    public static string Mapper_Everyone => Get(nameof(Mapper_Everyone));
    public static string Mapper_And => Get(nameof(Mapper_And));
    public static string Mapper_SplitEqual => Get(nameof(Mapper_SplitEqual));
    public static string Mapper_SplitWeighted => Get(nameof(Mapper_SplitWeighted));
    public static string Mapper_SplitByPercentage => Get(nameof(Mapper_SplitByPercentage));
    public static string Mapper_SplitCustomAmounts => Get(nameof(Mapper_SplitCustomAmounts));
    public static string Mapper_SplitCustomAmountsOnly => Get(nameof(Mapper_SplitCustomAmountsOnly));
    public static string Mapper_HouseholdOf => Get(nameof(Mapper_HouseholdOf));
    public static string Mapper_PaymentTitle => Get(nameof(Mapper_PaymentTitle));
    public static string Mapper_PaymentPrimaryText => Get(nameof(Mapper_PaymentPrimaryText));
    public static string Mapper_PaymentSecondaryText => Get(nameof(Mapper_PaymentSecondaryText));
    public static string Mapper_ActivityExpenseTitle => Get(nameof(Mapper_ActivityExpenseTitle));
    public static string Mapper_ActivityPaymentTitle => Get(nameof(Mapper_ActivityPaymentTitle));
    public static string Mapper_ActivityPaymentDetail => Get(nameof(Mapper_ActivityPaymentDetail));
    public static string Mapper_BalanceOwes => Get(nameof(Mapper_BalanceOwes));
    public static string Mapper_BalanceEvenNow => Get(nameof(Mapper_BalanceEvenNow));
    public static string Mapper_SummaryReadyToGo => Get(nameof(Mapper_SummaryReadyToGo));
    public static string Mapper_SummaryEvents => Get(nameof(Mapper_SummaryEvents));
    public static string Mapper_Today => Get(nameof(Mapper_Today));
    public static string Mapper_Yesterday => Get(nameof(Mapper_Yesterday));

    // --- Group status labels ---
    public static string Status_CurrentGroup => Get(nameof(Status_CurrentGroup));
    public static string Status_OpenedOn => Get(nameof(Status_OpenedOn));
    public static string Status_RecentActivity => Get(nameof(Status_RecentActivity));
    public static string Status_ReadyForFirstEvent => Get(nameof(Status_ReadyForFirstEvent));
    public static string Status_AddFirstEvent => Get(nameof(Status_AddFirstEvent));
    public static string Status_Settled => Get(nameof(Status_Settled));
    public static string Status_DefaultGroupName => Get(nameof(Status_DefaultGroupName));
    public static string Status_Household => Get(nameof(Status_Household));
}
