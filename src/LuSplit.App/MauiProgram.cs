using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Features.Groups.ArchivedGroups;
using LuSplit.App.Features.Groups.ArchivedGroupView;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Features.Groups.GroupSwitcher;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Features.Home.Home;
using LuSplit.App.Features.Payments.RecordPayment;
using LuSplit.App.Features.Payments.Settlement;
using LuSplit.App.Features.Settings.LanguageSettings;
using LuSplit.App.Services.Localization;
using LuSplit.App.Services.Persistence;
using Microsoft.Extensions.Logging;
using Plugin.MauiMtAdmob;

namespace LuSplit.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		LocalizationHelper.ApplyPersistedLanguage();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiMTAdmob()
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "BrandRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "BrandMedium");
				fonts.AddFont("OpenSans-Semibold.ttf", "BrandSemiBold");
            });

		builder.Services.AddSingleton<AppDataService>();
		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<GroupSwitcherPage>();
		builder.Services.AddTransient<GroupPage>();
		builder.Services.AddTransient<GroupDetailsPage>();
		builder.Services.AddTransient<AddExpensePage>();
		builder.Services.AddTransient<ExpenseDetailsPage>();
		builder.Services.AddTransient<SettlementPage>();
		builder.Services.AddTransient<RecordPaymentPage>();
		builder.Services.AddTransient<LanguageSettingsPage>();
		builder.Services.AddTransient<ArchivedGroupsPage>();
		builder.Services.AddTransient<ArchivedGroupViewPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
