using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Compass.Models;
using Compass.Services;
using Compass.Sync;
using Compass.Ui;
using Microsoft.Identity.Client;

namespace Compass.Views;

public partial class SettingsView : UserControl
{
    private readonly MainWindow _win;
    private DeviceCodeWindow? _deviceWin;

    public SettingsView(MainWindow win)
    {
        InitializeComponent();
        _win = win;

        // AI status + model
        var s = DataStore.Instance.Data.Settings;
        ClaudeStatus.Text = ClaudeService.IsAvailable()
            ? "✓ Connected to your Claude subscription through the Claude Code CLI — no API key or extra cost."
            : "⚠ Claude Code CLI not found. Install it and sign in with your subscription, then reopen Compass.";

        ModelBox.ItemsSource = new[] { "sonnet", "opus", "haiku" };
        ModelBox.SelectedItem = new[] { "sonnet", "opus", "haiku" }.Contains(s.ClaudeModel) ? s.ClaudeModel : "sonnet";
        ModelBox.SelectionChanged += (_, _) =>
        {
            s.ClaudeModel = ModelBox.SelectedItem as string ?? "sonnet";
            DataStore.Instance.Save();
        };

        DaysBox.ItemsSource = new[] { "7 days", "14 days", "30 days", "60 days", "90 days" };
        DaysBox.SelectedItem = s.ImportDays switch
        {
            <= 7 => "7 days",
            <= 14 => "14 days",
            <= 30 => "30 days",
            <= 60 => "60 days",
            _ => "90 days"
        };
        DaysBox.SelectionChanged += (_, _) =>
        {
            s.ImportDays = int.Parse((DaysBox.SelectedItem as string ?? "14 days").Split(' ')[0]);
            DataStore.Instance.Save();
        };

        LastImportLbl.Text = s.LastImport is DateTime t
            ? "Last import: " + t.ToString("ddd d MMM, h:mm tt")
            : "No imports yet.";

        BuildAccounts();
        UpdateSyncUi();
    }

    // ---- Sync with phone ----

    private void UpdateSyncUi()
    {
        var s = DataStore.Instance.Data.Settings;
        bool signedIn = SyncService.Instance.IsSignedIn(s);
        SyncSignedInPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        SyncSignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        SyncStatus.Text = signedIn
            ? $"✓ Signed in as {s.SyncEmail}. Your deadlines sync automatically."
            : "Not signed in. Deadlines stay on this PC only until you sign in.";
        if (signedIn)
            LastSyncLbl.Text = s.LastSyncUtc is DateTime u
                ? "Last synced: " + u.ToLocalTime().ToString("ddd d MMM, h:mm tt")
                : "Not synced yet.";
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        await DoAuth(signUp: false);
    }

    private async void CreateAccount_Click(object sender, RoutedEventArgs e)
    {
        await DoAuth(signUp: true);
    }

    private async Task DoAuth(bool signUp)
    {
        string email = SyncEmailBox.Text.Trim();
        string pw = SyncPwBox.Password;
        if (email.Length == 0 || pw.Length == 0) { SyncStatus.Text = "Enter your email and a password first."; return; }

        var s = DataStore.Instance.Data.Settings;
        SyncStatus.Text = signUp ? "Creating account…" : "Signing in…";
        try
        {
            string msg = signUp
                ? await SyncService.Instance.SignUpAsync(email, pw, s)
                : await SyncService.Instance.SignInAsync(email, pw, s);
            DataStore.Instance.Save(touch: false);
            SyncPwBox.Clear();
            UpdateSyncUi();
            SyncStatus.Text = msg;
            if (SyncService.Instance.IsSignedIn(s)) await RunSync();
        }
        catch (Exception ex)
        {
            SyncStatus.Foreground = UiKit.B("Red");
            SyncStatus.Text = "✕ " + ex.Message;
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e) => await RunSync();

    private async Task RunSync()
    {
        LastSyncLbl.Text = "Syncing…";
        var r = await SyncService.Instance.SyncAsync(DataStore.Instance.Data);
        DataStore.Instance.Save(touch: false);
        UpdateSyncUi();
        LastSyncLbl.Text = r.Message + (DataStore.Instance.Data.Settings.LastSyncUtc is DateTime u
            ? "  ·  " + u.ToLocalTime().ToString("h:mm tt") : "");
        if (r.Outcome == SyncOutcome.AdoptedRemote) _win.Refresh();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        SyncService.Instance.SignOut(DataStore.Instance.Data.Settings);
        DataStore.Instance.Save(touch: false);
        UpdateSyncUi();
    }

    private void BuildAccounts()
    {
        AccountsPanel.Children.Clear();
        foreach (var acct in DataStore.Instance.Data.Settings.Accounts)
            AccountsPanel.Children.Add(acct.AuthType == "oauth" ? BuildOAuth(acct) : BuildPassword(acct));
    }

    // ---- helpers ----

    private static TextBlock Head(string label, string email)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
        tb.Inlines.Add(new System.Windows.Documents.Run(label + "   ")
        { Foreground = UiKit.B("Text"), FontWeight = FontWeights.Bold, FontSize = 16 });
        tb.Inlines.Add(new System.Windows.Documents.Run(email)
        { Foreground = UiKit.B("Muted"), FontSize = 13 });
        return tb;
    }

    private static Border Card(UIElement child) => new()
    {
        Background = UiKit.B("Surface"),
        BorderBrush = UiKit.B("Line"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(18, 14, 18, 16),
        Margin = new Thickness(0, 0, 0, 12),
        Child = child,
    };

    private static Button Btn(string text, string style, RoutedEventHandler onClick)
    {
        var b = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources[style],
            FontSize = 12.5,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0),
        };
        b.Click += onClick;
        return b;
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ---- Gmail / password accounts ----

    private Border BuildPassword(EmailAccount acct)
    {
        var panel = new StackPanel();
        panel.Children.Add(Head(acct.Label, acct.Email));

        bool hasPw = !string.IsNullOrEmpty(acct.SecretEnc);
        var status = new TextBlock
        {
            Text = acct.LastStatus.Length > 0 ? acct.LastStatus : (hasPw ? "App password saved ✓" : "No app password yet."),
            Foreground = UiKit.B("Muted"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8),
        };

        var pwBox = new PasswordBox { Width = 240, Height = 32, FontSize = 13, Padding = new Thickness(8, 5, 8, 5), VerticalContentAlignment = VerticalAlignment.Center };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(pwBox);
        row.Children.Add(new Border { Width = 8 });
        row.Children.Add(Btn("Save", "Ghost", (_, _) =>
        {
            if (pwBox.Password.Length == 0) { status.Text = "Type the app password first."; return; }
            acct.SecretEnc = Crypto.Protect(pwBox.Password.Replace(" ", ""));
            acct.LastStatus = "App password saved ✓";
            DataStore.Instance.Save();
            status.Text = acct.LastStatus;
            pwBox.Clear();
        }));
        row.Children.Add(Btn("Test", "Ghost", async (_, _) => await TestAccount(acct, status)));

        panel.Children.Add(row);
        panel.Children.Add(status);

        var help = new TextBlock
        {
            Foreground = UiKit.B("Muted"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 6),
            Text = "Gmail needs an “App Password” (not your normal password). Turn on 2-Step Verification, then create one and paste it above.",
        };
        panel.Children.Add(help);
        panel.Children.Add(Btn("Open Google App Passwords", "Ghost", (_, _) => OpenUrl("https://myaccount.google.com/apppasswords")));

        return Card(panel);
    }

    // ---- BINUS / Microsoft OAuth ----

    private Border BuildOAuth(EmailAccount acct)
    {
        var panel = new StackPanel();
        panel.Children.Add(Head(acct.Label, acct.Email));

        var status = new TextBlock
        {
            Text = acct.LastStatus.Length > 0 ? acct.LastStatus
                : (string.IsNullOrWhiteSpace(acct.OAuthClientId) ? "Needs a Microsoft app (client) ID — see setup steps." : "Client ID saved. Click ‘Connect / sign in’."),
            Foreground = UiKit.B("Muted"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8),
        };

        var idBox = new TextBox
        {
            Text = acct.OAuthClientId,
            Width = 340,
            Height = 32,
            FontSize = 13,
            Padding = new Thickness(8, 5, 8, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row1.Children.Add(idBox);
        row1.Children.Add(new Border { Width = 8 });
        row1.Children.Add(Btn("Save ID", "Ghost", (_, _) =>
        {
            acct.OAuthClientId = idBox.Text.Trim();
            DataStore.Instance.Save();
            status.Text = "Client ID saved. Click ‘Connect / sign in’.";
        }));
        panel.Children.Add(row1);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row2.Children.Add(Btn("Connect / sign in", "Primary", async (_, _) => await TestAccount(acct, status)));
        row2.Children.Add(Btn("Setup steps", "Ghost", (_, _) => ShowAzureSteps()));
        panel.Children.Add(row2);

        panel.Children.Add(status);

        panel.Children.Add(new TextBlock
        {
            Foreground = UiKit.B("Muted"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Text = "BINUS runs on Microsoft 365, which needs a Microsoft sign-in (not a password). This requires a one-time app registration — click ‘Setup steps’. Some universities block this; if so, forward BINUS mail to Gmail instead.",
        });

        return Card(panel);
    }

    private void ShowAzureSteps()
    {
        string steps =
            "Set up BINUS (one time, ~5 minutes):\n\n" +
            "1. Go to entra.microsoft.com → ‘App registrations’ → ‘New registration’.\n" +
            "2. Name it ‘Compass’. Supported accounts: ‘Accounts in any organizational directory’. Register.\n" +
            "3. Copy the ‘Application (client) ID’, paste it into Compass, and click ‘Save ID’.\n" +
            "4. In the app → ‘Authentication’ → turn ON ‘Allow public client flows’ → Save.\n" +
            "5. ‘API permissions’ → Add a permission → ‘APIs my organization uses’ → ‘Office 365 Exchange Online’ → Delegated → tick ‘IMAP.AccessAsUser.All’ → Add.\n" +
            "6. Back in Compass, click ‘Connect / sign in’ and log in with your @binus.ac.id account.\n\n" +
            "If your university blocks app registration or requires IT approval, use the forward-BINUS-to-Gmail approach instead.";

        var r = MessageBox.Show(steps + "\n\nOpen the Microsoft registration page now?",
            "BINUS setup", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (r == MessageBoxResult.Yes) OpenUrl("https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade");
    }

    // ---- device code + connection test ----

    private Task OnDeviceCode(DeviceCodeResult r)
    {
        Dispatcher.Invoke(() =>
        {
            _deviceWin = new DeviceCodeWindow(r.Message, r.VerificationUrl, r.UserCode) { Owner = Window.GetWindow(this) };
            _deviceWin.Show();
        });
        return Task.CompletedTask;
    }

    private void CloseDeviceWin() => Dispatcher.Invoke(() => { _deviceWin?.Close(); _deviceWin = null; });

    private async Task TestAccount(EmailAccount acct, TextBlock status)
    {
        status.Foreground = UiKit.B("Muted");
        status.Text = "Connecting…";
        try
        {
            string msg = await new EmailService().TestAsync(acct, OnDeviceCode);
            acct.LastStatus = msg;
            DataStore.Instance.Save();
            status.Foreground = UiKit.B("Green");
            status.Text = "✓ " + msg;
        }
        catch (Exception ex)
        {
            acct.LastStatus = "Failed: " + ex.Message;
            DataStore.Instance.Save();
            status.Foreground = UiKit.B("Red");
            status.Text = "✕ " + ex.Message;
        }
        finally { CloseDeviceWin(); }
    }

    // ---- import ----

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var s = DataStore.Instance.Data.Settings;
        var accounts = s.Accounts.Where(a => a.Enabled).ToList();
        if (accounts.Count == 0) { ImportStatus.Text = "No accounts configured."; return; }
        if (!ClaudeService.IsAvailable())
        {
            MessageBox.Show("The AI (Claude Code CLI) isn't available, so email can't be scanned for dates. See the AI section above.",
                "Compass", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ImportBtn.IsEnabled = false;
        ImportStatus.Foreground = UiKit.B("Muted");

        try
        {
            var (scan, errors) = await new EmailImporter().RunAsync(
                s.ImportDays, OnDeviceCode, msg => ImportStatus.Text = msg);
            CloseDeviceWin();

            s.LastImport = DateTime.Now;
            DataStore.Instance.Save();
            LastImportLbl.Text = "Last import: " + s.LastImport.Value.ToString("ddd d MMM, h:mm tt");

            if (scan.EmailCount == 0)
            {
                ImportStatus.Foreground = UiKit.B("Amber");
                ImportStatus.Text = errors.Count > 0
                    ? "Couldn't read email — " + string.Join(" | ", errors)
                    : "No recent emails found.";
                return;
            }

            string note = errors.Count > 0 ? "  (some accounts failed: " + string.Join(" | ", errors) + ")" : "";
            ImportStatus.Foreground = UiKit.B("Muted");
            ImportStatus.Text = $"Found {scan.Deadlines.Count} deadline(s) and {scan.Actions.Count} action item(s)." + note;

            var dlg = new ImportReviewDialog(scan) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
            _win.Refresh();
        }
        catch (Exception ex)
        {
            ImportStatus.Foreground = UiKit.B("Red");
            ImportStatus.Text = "Import failed: " + ex.Message;
        }
        finally
        {
            ImportBtn.IsEnabled = true;
            CloseDeviceWin();
        }
    }
}
