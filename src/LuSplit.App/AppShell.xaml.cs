using LuSplit.App.Features.Activity;
using LuSplit.App.Features.Auth;
using LuSplit.App.Features.Devices;
using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Features.Groups.ArchivedGroups;
using LuSplit.App.Features.Groups.CreateGroup;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Features.Invitations;
using LuSplit.App.Features.Members;
using LuSplit.App.Features.Payments.RecordPayment;
using LuSplit.App.Features.Payments.Settlement;
using LuSplit.App.Features.SharedGroups;

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
			//TryRegisterRoute(AppRoutes.Settings, typeof(SettingsPage));
			//TryRegisterRoute(AppRoutes.ArchivedGroups, typeof(ArchivedGroupsPage));
			TryRegisterRoute(AppRoutes.InvitationLanding, typeof(InvitationLandingPage));
			TryRegisterRoute(AppRoutes.Authentication, typeof(AuthenticationPage));
			TryRegisterRoute(AppRoutes.ShareGroup, typeof(ShareGroupPage));
			TryRegisterRoute(AppRoutes.MemberList, typeof(MemberListPage));
			TryRegisterRoute(AppRoutes.DeviceManagement, typeof(DeviceManagementPage));
			TryRegisterRoute(AppRoutes.ActivityFeed, typeof(ActivityFeedPage));
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
