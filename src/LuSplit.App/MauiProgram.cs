using LuSplit.App.Pages;
using LuSplit.App.Services;
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
			});

		builder.Services.AddSingleton<AppDataService>();
		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<TripPage>();
		builder.Services.AddTransient<TripDetailsPage>();
		builder.Services.AddTransient<ActivityPage>();
		builder.Services.AddTransient<AddExpensePage>();
		builder.Services.AddTransient<SettlementPage>();
		builder.Services.AddTransient<RecordPaymentPage>();
		builder.Services.AddTransient<LanguageSettingsPage>();
		builder.Services.AddTransient<ArchivedTripsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
