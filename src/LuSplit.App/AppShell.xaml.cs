using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Features.Groups.ArchivedGroups;
using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Features.Payments.RecordPayment;
using LuSplit.App.Features.Payments.Settlement;

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

			//TryRegisterRoute(AppRoutes.Home, typeof(HomePage));
			//TryRegisterRoute(AppRoutes.GroupSwitcher, typeof(GroupSwitcherPage));
			TryRegisterRoute(AppRoutes.CreateGroup, typeof(CreateGroupPage));
			TryRegisterRoute(AppRoutes.GroupTimeline, typeof(GroupPage));
			TryRegisterRoute(AppRoutes.GroupDetails, typeof(GroupDetailsPage));
			TryRegisterRoute(AppRoutes.AddExpense, typeof(AddExpensePage));
			TryRegisterRoute(AppRoutes.ExpenseDetails, typeof(ExpenseDetailsPage));
			TryRegisterRoute(AppRoutes.RecordPayment, typeof(RecordPaymentPage));
			TryRegisterRoute(AppRoutes.Settlement, typeof(SettlementPage));
			//TryRegisterRoute(AppRoutes.LanguageSettings, typeof(LanguageSettingsPage));
			//TryRegisterRoute(AppRoutes.ArchivedGroups, typeof(ArchivedGroupsPage));
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
