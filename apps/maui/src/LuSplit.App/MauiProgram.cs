using Microsoft.Extensions.Logging;
using LuSplit.App.Pages;
using LuSplit.App.Services;

namespace LuSplit.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "BrandRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "BrandMedium");
			});

		builder.Services.AddSingleton<AppDataService>();
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<AddExpensePage>();
		builder.Services.AddTransient<SettlementPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
