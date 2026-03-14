using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using LuSplit.App.Services;

namespace LuSplit.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;

	public App()
		: this(IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Missing platform service provider."))
	{
	}

	public App(IServiceProvider services)
	{
		_services = services;
		InitializeComponent();
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