using Compass.Mobile.Services;
using Compass.Sync;

namespace Compass.Mobile.Pages;

public partial class SyncPage : ContentPage
{
    public SyncPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateUi();
    }

    private void UpdateUi()
    {
        var s = MobileStore.Instance.Data.Settings;
        bool signedIn = SyncService.Instance.IsSignedIn(s);
        SignedIn.IsVisible = signedIn;
        SignedOut.IsVisible = !signedIn;
        Status.Text = signedIn
            ? $"✓ Signed in as {s.SyncEmail}. Deadlines sync automatically."
            : "Not signed in. Your deadlines stay on this phone only until you sign in.";
        if (signedIn)
            LastSync.Text = s.LastSyncUtc is DateTime u
                ? "Last synced: " + u.ToLocalTime().ToString("ddd d MMM, h:mm tt")
                : "Not synced yet.";
    }

    private async void SignIn_Clicked(object? sender, EventArgs e) => await DoAuth(false);
    private async void Create_Clicked(object? sender, EventArgs e) => await DoAuth(true);

    private async Task DoAuth(bool signUp)
    {
        string email = (EmailBox.Text ?? "").Trim();
        string pw = PwBox.Text ?? "";
        if (email.Length == 0 || pw.Length == 0) { Status.Text = "Enter your email and password first."; return; }

        var s = MobileStore.Instance.Data.Settings;
        Status.Text = signUp ? "Creating account…" : "Signing in…";
        try
        {
            string msg = signUp
                ? await SyncService.Instance.SignUpAsync(email, pw, s)
                : await SyncService.Instance.SignInAsync(email, pw, s);
            MobileStore.Instance.Save(touch: false);
            PwBox.Text = "";
            UpdateUi();
            Status.Text = msg;
            if (SyncService.Instance.IsSignedIn(s)) await RunSync();
        }
        catch (Exception ex)
        {
            Status.Text = "✕ " + ex.Message;
        }
    }

    private async void SyncNow_Clicked(object? sender, EventArgs e) => await RunSync();

    private async Task RunSync()
    {
        LastSync.Text = "Syncing…";
        var r = await MobileSync.RunAsync();
        UpdateUi();
        LastSync.Text = r.Message;
    }

    private void SignOut_Clicked(object? sender, EventArgs e)
    {
        SyncService.Instance.SignOut(MobileStore.Instance.Data.Settings);
        MobileStore.Instance.Save(touch: false);
        UpdateUi();
    }
}
