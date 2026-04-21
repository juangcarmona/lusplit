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
		var dataService = _services.GetRequiredService<AppDataService>();
		await dataService.InitializeAsync();
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
	}
}
