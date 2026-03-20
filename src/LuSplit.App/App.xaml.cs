using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using LuSplit.App.Services;

namespace LuSplit.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;

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
		Application.Current!.UserAppTheme = AppPreferences.IsDarkThemeEnabled() ? AppTheme.Dark : AppTheme.Light;
	}

	protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
	{
		_ = InitializeAsync();
		return new Microsoft.Maui.Controls.Window(_services.GetRequiredService<AppShell>());
	}

	private async Task InitializeAsync()
	{
		var dataService = _services.GetRequiredService<AppDataService>();
		await dataService.InitializeAsync();
	}
}
