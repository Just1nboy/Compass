using System.Threading;

namespace Compass.Mobile;

public partial class App : Application
{
	// Coalesces a burst of edits into a single sync ~1.5s after the last change.
	private CancellationTokenSource? _syncDebounce;

	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;

		// Background auto-sync: once at launch, then every 90 seconds as a safety net.
		_ = Compass.Mobile.Services.MobileSync.RunAsync();
		Dispatcher.StartTimer(TimeSpan.FromSeconds(90), () =>
		{
			_ = Compass.Mobile.Services.MobileSync.RunAsync();
			return true;
		});

		// Instant sync: whenever anything changes locally, push it to the cloud right away
		// (debounced). This is what makes it feel automatic — no need to ever tap "Sync now".
		Compass.Mobile.Services.MobileStore.Instance.Changed += OnLocalChanged;
	}

	private void OnLocalChanged()
	{
		_syncDebounce?.Cancel();
		var cts = _syncDebounce = new CancellationTokenSource();
		_ = Task.Delay(1500, cts.Token).ContinueWith(t =>
		{
			if (!t.IsCanceled)
				_ = Compass.Mobile.Services.MobileSync.RunAsync();
		}, TaskScheduler.Default);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		// On resume, reload from disk so notes captured via the home-screen widget show up, then sync.
		window.Resumed += (_, _) =>
		{
			Compass.Mobile.Services.MobileStore.Instance.Load();
			_ = Compass.Mobile.Services.MobileSync.RunAsync();
		};
		return window;
	}
}