namespace LuSplit.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		TryRegisterRoute(AppRoutes.TripTimeline, typeof(Pages.TripPage));
		TryRegisterRoute(AppRoutes.TripDetails, typeof(Pages.TripDetailsPage));
		TryRegisterRoute(AppRoutes.AddEvent, typeof(Pages.AddExpensePage));
		TryRegisterRoute(AppRoutes.RecordPayment, typeof(Pages.RecordPaymentPage));
		TryRegisterRoute(AppRoutes.Settlement, typeof(Pages.SettlementPage));
		TryRegisterRoute(AppRoutes.LanguageSettings, typeof(Pages.LanguageSettingsPage));
	}

	// Guard against duplicate registration when the shell is rebuilt for language changes.
	private static void TryRegisterRoute(string route, Type pageType)
	{
		try { Routing.RegisterRoute(route, pageType); }
		catch (ArgumentException) { /* already registered – safe to ignore */ }
	}
}
