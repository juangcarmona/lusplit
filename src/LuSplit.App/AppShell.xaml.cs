namespace LuSplit.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		TryRegisterRoute(AppRoutes.Home, typeof(Pages.HomePage));
		TryRegisterRoute(AppRoutes.History, typeof(Pages.ActivityPage));
		TryRegisterRoute(AppRoutes.GroupTimeline, typeof(Pages.GroupPage));
		TryRegisterRoute(AppRoutes.GroupDetails, typeof(Pages.GroupDetailsPage));
		TryRegisterRoute(AppRoutes.AddEvent, typeof(Pages.AddExpensePage));
		TryRegisterRoute(AppRoutes.RecordPayment, typeof(Pages.RecordPaymentPage));
		TryRegisterRoute(AppRoutes.Settlement, typeof(Pages.SettlementPage));
		TryRegisterRoute(AppRoutes.LanguageSettings, typeof(Pages.LanguageSettingsPage));
		TryRegisterRoute(AppRoutes.ArchivedGroups, typeof(Pages.ArchivedGroupsPage));
	}

	// Guard against duplicate registration when the shell is rebuilt for language changes.
	private static void TryRegisterRoute(string route, Type pageType)
	{
		try { Routing.RegisterRoute(route, pageType); }
		catch (ArgumentException) { /* already registered – safe to ignore */ }
	}
}
