using LuSplit.App.Services;
using LuSplit.App.Services.Persistence;
using LuSplit.App.Services.Settings;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LuSplit.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private CancellationTokenSource? _syncCts;

	/// <summary>Exposed so LocalizationHelper can resolve AppShell on UI rebuild.</summary>
	public static IServiceProvider? Services { get; private set; }

	public App()
		: this(IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Missing platform service provider."))
	{
	}

	public App(IServiceProvider services)
	{
		_services = services;
		Services = services;
		InitializeComponent();
		MauiApplication.Current!.UserAppTheme = AppPreferences.IsDarkThemeEnabled() ? AppTheme.Dark : AppTheme.Light;
	}

	protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
	{
		_ = InitializeAsync();
		return new Microsoft.Maui.Controls.Window(_services.GetRequiredService<AppShell>());
	}

	protected override void OnResume()
	{
		base.OnResume();
		// Rehydrate auth state — catches browser-redirect returns after process death.
		var session = _services.GetRequiredService<SessionService>();
		_ = session.RefreshAsync();
		_syncCts = new CancellationTokenSource();
		_ = TriggerBackgroundSyncAsync(_syncCts.Token);
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		_syncCts?.Cancel();
		_syncCts?.Dispose();
		_syncCts = null;
	}

	private async Task InitializeAsync()
	{
		try
		{
			var dataService = _services.GetRequiredService<AppDataService>();
			await dataService.InitializeAsync();

			// Rehydrate auth session from MSAL token cache + local store.
			// Handles process-death recovery after Android browser redirect.
			var session = _services.GetRequiredService<SessionService>();
			await session.RefreshAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[LuSplit] InitializeAsync failed: {ex}");
		}
	}

	private async Task TriggerBackgroundSyncAsync(CancellationToken ct)
	{
		try
		{
			var dataService = _services.GetRequiredService<AppDataService>();
			var groupIds = await dataService.GetSharedGroupIdsAsync(ct);
			if (groupIds.Count == 0) return;

			var syncService = _services.GetRequiredService<SyncOrchestrationService>();
			await syncService.SyncAllAsync(groupIds, ct);
		}
		catch (OperationCanceledException)
		{
			// App went to sleep — expected.
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[LuSplit] Background sync failed: {ex}");
		}
	}
}
