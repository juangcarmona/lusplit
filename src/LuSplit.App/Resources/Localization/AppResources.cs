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

    // --- Trips (Home) ---
    public static string Trips_Title => Get(nameof(Trips_Title));
    public static string Trips_Subtitle => Get(nameof(Trips_Subtitle));
    public static string Trips_EmptyTitle => Get(nameof(Trips_EmptyTitle));
    public static string Trips_EmptySubtitle => Get(nameof(Trips_EmptySubtitle));
    public static string Trips_Open => Get(nameof(Trips_Open));
    public static string Trips_Details => Get(nameof(Trips_Details));
    public static string Trips_NewTrip => Get(nameof(Trips_NewTrip));
    public static string Trips_Active => Get(nameof(Trips_Active));

    // --- Trip timeline ---
    public static string Trip_Title => Get(nameof(Trip_Title));
    public static string Trip_DetailsButton => Get(nameof(Trip_DetailsButton));
    public static string Trip_WhoOwesWhat => Get(nameof(Trip_WhoOwesWhat));
    public static string Trip_EveryoneEven => Get(nameof(Trip_EveryoneEven));
    public static string Trip_SettleUp => Get(nameof(Trip_SettleUp));
    public static string Trip_Events => Get(nameof(Trip_Events));
    public static string Trip_EmptyTitle => Get(nameof(Trip_EmptyTitle));
    public static string Trip_EmptySubtitle => Get(nameof(Trip_EmptySubtitle));
    public static string Trip_AddExpense => Get(nameof(Trip_AddExpense));
    public static string Trip_RecordPayment => Get(nameof(Trip_RecordPayment));

    // --- Trip Details ---
    public static string TripDetails_Title => Get(nameof(TripDetails_Title));
    public static string TripDetails_PageTitleCreate => Get(nameof(TripDetails_PageTitleCreate));
    public static string TripDetails_PageTitleEdit => Get(nameof(TripDetails_PageTitleEdit));
    public static string TripDetails_SubtitleCreate => Get(nameof(TripDetails_SubtitleCreate));
    public static string TripDetails_SubtitleEdit => Get(nameof(TripDetails_SubtitleEdit));
    public static string TripDetails_TripNameLabel => Get(nameof(TripDetails_TripNameLabel));
    public static string TripDetails_TripNamePlaceholder => Get(nameof(TripDetails_TripNamePlaceholder));
    public static string TripDetails_CurrencyLabel => Get(nameof(TripDetails_CurrencyLabel));
    public static string TripDetails_PeopleSection => Get(nameof(TripDetails_PeopleSection));
    public static string TripDetails_HouseholdHint => Get(nameof(TripDetails_HouseholdHint));
    public static string TripDetails_NoPeopleTitle => Get(nameof(TripDetails_NoPeopleTitle));
    public static string TripDetails_NoPeopleSubtitle => Get(nameof(TripDetails_NoPeopleSubtitle));
    public static string TripDetails_AddPersonSection => Get(nameof(TripDetails_AddPersonSection));
    public static string TripDetails_PersonNamePlaceholder => Get(nameof(TripDetails_PersonNamePlaceholder));
    public static string TripDetails_HouseholdNamePlaceholder => Get(nameof(TripDetails_HouseholdNamePlaceholder));
    public static string TripDetails_AddPersonButton => Get(nameof(TripDetails_AddPersonButton));
    public static string TripDetails_CreateButton => Get(nameof(TripDetails_CreateButton));
    public static string TripDetails_SaveButton => Get(nameof(TripDetails_SaveButton));
    public static string TripDetails_ExportButton => Get(nameof(TripDetails_ExportButton));
    public static string TripDetails_SettlesOnOwn => Get(nameof(TripDetails_SettlesOnOwn));
    public static string TripDetails_PersonAdded => Get(nameof(TripDetails_PersonAdded));
    public static string TripDetails_PersonAddedNew => Get(nameof(TripDetails_PersonAddedNew));
    public static string TripDetails_Remove => Get(nameof(TripDetails_Remove));

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
    public static string Validation_TripNotFound => Get(nameof(Validation_TripNotFound));
    public static string Validation_TripNameRequired => Get(nameof(Validation_TripNameRequired));
    public static string Validation_SelectCurrency => Get(nameof(Validation_SelectCurrency));
    public static string Validation_AddAtLeastOnePerson => Get(nameof(Validation_AddAtLeastOnePerson));
    public static string Validation_ChooseBothPeople => Get(nameof(Validation_ChooseBothPeople));
    public static string Validation_DifferentPeople => Get(nameof(Validation_DifferentPeople));

    // --- Settings ---
    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string Settings_LanguageSection => Get(nameof(Settings_LanguageSection));
    public static string Settings_LanguageHint => Get(nameof(Settings_LanguageHint));
    public static string Settings_LanguageSaved => Get(nameof(Settings_LanguageSaved));
    public static string Language_SystemDefault => Get(nameof(Language_SystemDefault));
}
