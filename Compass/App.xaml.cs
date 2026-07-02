using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Compass.Services;
using Compass.Sync;
using WF = System.Windows.Forms;

namespace Compass;

public partial class App : Application
{
    private Mutex? _mutex;
    private WF.NotifyIcon? _tray;
    private ReminderService? _reminders;
    private DispatcherTimer? _syncTimer;
    private DispatcherTimer? _syncDebounce;
    private bool _syncing;

    public static MainWindow? MainWin { get; private set; }
    public static WF.NotifyIcon? Tray { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Never fail silently again: log any unhandled error and tell the user, instead of
        // the window just vanishing. UI-thread errors are marked Handled so the app survives.
        DispatcherUnhandledException += (_, ex) =>
        {
            LogError("UI", ex.Exception);
            MessageBox.Show(
                "Compass hit an unexpected error but is still running:\n\n" + ex.Exception.Message +
                "\n\n(Details saved to error-log.txt in your Compass data folder.)",
                "Compass", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            LogError("Task", ex.Exception);
            ex.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            LogError("Fatal", ex.ExceptionObject as Exception);

        // Single instance — since we auto-start, a second launch just surfaces the first.
        _mutex = new Mutex(true, "Compass_SingleInstance_9F2C", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        DataStore.Instance.Load();
        StartupManager.EnsureAutoStart();

        SetupTray();

        MainWin = new MainWindow();
        MainWin.Show();

        _reminders = new ReminderService(_tray!);
        _reminders.Start();

        // Auto-sync with the phone: an initial pass shortly after launch, then periodically
        // as a safety net. The real work is event-driven — see below.
        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
        _syncTimer.Tick += async (_, _) => await SyncTick();
        _syncTimer.Start();
        Dispatcher.InvokeAsync(async () => { await Task.Delay(3000); await SyncTick(); });

        // Instant sync: whenever anything changes locally, push it to the cloud right away
        // (debounced ~1.5s so a burst of edits collapses into one sync). This is what makes
        // it feel automatic — no need to ever tap "Sync".
        _syncDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _syncDebounce.Tick += async (_, _) => { _syncDebounce!.Stop(); await SyncTick(); };
        DataStore.Instance.Changed += () =>
        {
            _syncDebounce!.Stop();
            _syncDebounce!.Start();
        };

        // Proactively scan email a few seconds after launch (throttled, runs in background).
        Dispatcher.InvokeAsync(async () => { await Task.Delay(6000); await AutoScanEmailAsync(); });
    }

    private static void LogError(string kind, Exception? ex)
    {
        if (ex == null) return;
        try
        {
            string path = Path.Combine(DataStore.DataFolder, "error-log.txt");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    private async Task AutoScanEmailAsync()
    {
        var s = DataStore.Instance.Data.Settings;
        if (!s.Accounts.Any(a => a.Enabled)) return;
        if (!ClaudeService.IsAvailable()) return;

        // Don't re-scan more than once every 6 hours.
        if (s.LastImport is DateTime last && (DateTime.Now - last).TotalHours < 6) return;

        try
        {
            // No device-code prompt for a background run — OAuth accounts without a cached token just skip.
            var (scan, _) = await new EmailImporter().RunAsync(s.ImportDays, null, null);
            s.LastImport = DateTime.Now;

            int added = EmailImporter.AddActionsToInbox(scan.Actions);
            DataStore.Instance.Save();
            MainWin?.Refresh();

            if (added > 0 || scan.Deadlines.Count > 0)
            {
                string msg = "";
                if (scan.Deadlines.Count > 0)
                    msg += $"{scan.Deadlines.Count} possible deadline(s) — open Settings ▸ Import now to review. ";
                if (added > 0)
                    msg += $"{added} new to-do(s) added to your inbox.";
                _tray?.ShowBalloonTip(15000, "📬 Found things in your email", msg.Trim(), WF.ToolTipIcon.Info);
            }
        }
        catch { }
    }

    private async Task SyncTick()
    {
        if (_syncing) return;
        var data = DataStore.Instance.Data;
        if (!SyncService.Instance.IsSignedIn(data.Settings)) return;

        _syncing = true;
        try
        {
            var r = await SyncService.Instance.SyncAsync(data);
            DataStore.Instance.Save(touch: false);
            if (r.Outcome == SyncOutcome.AdoptedRemote) MainWin?.Refresh();
        }
        catch { }
        finally { _syncing = false; }
    }

    private void SetupTray()
    {
        _tray = new WF.NotifyIcon
        {
            Icon = IconFactory.TrayIcon(),
            Visible = true,
            Text = "Compass — your deadlines & to-dos"
        };
        Tray = _tray;

        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Open Compass", null, (_, _) => ShowMain());
        menu.Items.Add("Add a deadline…", null, (_, _) => { ShowMain(); MainWin?.OpenAddDeadline(); });
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Quit Compass", null, (_, _) => QuitApp());
        _tray.ContextMenuStrip = menu;

        _tray.DoubleClick += (_, _) => ShowMain();
        _tray.BalloonTipClicked += (_, _) => ShowMain();
    }

    private void ShowMain()
    {
        MainWin ??= new MainWindow();
        MainWin.ShowFromTray();
        // Opening the window: pull anything the phone changed while we were in the tray.
        Dispatcher.InvokeAsync(async () => await SyncTick());
    }

    public void QuitApp()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
