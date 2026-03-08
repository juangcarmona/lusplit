namespace LuSplit.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(AppRoutes.TripTimeline, typeof(Pages.TripPage));
		Routing.RegisterRoute(AppRoutes.TripDetails, typeof(Pages.TripDetailsPage));
		Routing.RegisterRoute(AppRoutes.AddEvent, typeof(Pages.AddExpensePage));
		Routing.RegisterRoute(AppRoutes.RecordPayment, typeof(Pages.RecordPaymentPage));
	}
}
