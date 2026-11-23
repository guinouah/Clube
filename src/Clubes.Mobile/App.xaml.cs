using Microsoft.Extensions.DependencyInjection;

namespace Clubes.Mobile;

public partial class App : Application
{
	private readonly AppShell _appShell;
	private readonly Clubes.Mobile.Services.OfflineSyncCoordinator _offlineSync;

	public App(AppShell appShell, Clubes.Mobile.Services.OfflineSyncCoordinator offlineSync)
	{
		_appShell = appShell;
		_offlineSync = offlineSync;
		InitializeComponent();
		_ = _offlineSync.StartAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell);
	}
}
