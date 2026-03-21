namespace LuSplit.App;

public partial class AppShell : Shell
{
	private static bool _routesRegistered;
	private static readonly object RoutesRegistrationLock = new();

	public AppShell()
	{
		InitializeComponent();

		lock (RoutesRegistrationLock)
		{
			if (_routesRegistered)
			{
				return;
			}

			TryRegisterRoute(AppRoutes.Home, typeof(Pages.HomePage));
			TryRegisterRoute(AppRoutes.GroupSwitcher, typeof(Pages.GroupSwitcherPage));
			TryRegisterRoute(AppRoutes.CreateGroup, typeof(Pages.CreateGroupPage));
			TryRegisterRoute(AppRoutes.GroupTimeline, typeof(Pages.GroupPage));
			TryRegisterRoute(AppRoutes.GroupDetails, typeof(Pages.GroupDetailsPage));
			TryRegisterRoute(AppRoutes.AddExpense, typeof(Pages.AddExpensePage));
			TryRegisterRoute(AppRoutes.ExpenseDetails, typeof(Pages.ExpenseDetailsPage));
			TryRegisterRoute(AppRoutes.RecordPayment, typeof(Pages.RecordPaymentPage));
			TryRegisterRoute(AppRoutes.Settlement, typeof(Pages.SettlementPage));
			TryRegisterRoute(AppRoutes.LanguageSettings, typeof(Pages.LanguageSettingsPage));
			TryRegisterRoute(AppRoutes.ArchivedGroups, typeof(Pages.ArchivedGroupsPage));
			_routesRegistered = true;
		}
	}

	// Guard against duplicate registration when the shell is rebuilt for language changes.
	private static void TryRegisterRoute(string route, Type pageType)
	{
		try { Routing.RegisterRoute(route, pageType); }
		catch (ArgumentException) { /* already registered – safe to ignore */ }
	}
}
