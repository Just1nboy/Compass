using Compass.Sync;

namespace Compass.Mobile.Services;

/// <summary>Runs sync against the phone's local store, guarding against overlap.</summary>
public static class MobileSync
{
    private static bool _busy;

    /// <summary>
    /// Raised (on the UI thread) after a sync pulls new data down from the cloud,
    /// so whatever page is on screen can rebuild and show what changed on the PC.
    /// </summary>
    public static event Action? Synced;

    public static async Task<SyncResult> RunAsync()
    {
        var data = MobileStore.Instance.Data;
        if (_busy || !SyncService.Instance.IsSignedIn(data.Settings))
            return new SyncResult(SyncOutcome.NotSignedIn, "");

        _busy = true;
        try
        {
            var r = await SyncService.Instance.SyncAsync(data);
            MobileStore.Instance.Save(touch: false);
            if (r.Outcome == SyncOutcome.AdoptedRemote)
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                    () => Synced?.Invoke());
            return r;
        }
        catch (Exception ex)
        {
            return new SyncResult(SyncOutcome.Error, ex.Message);
        }
        finally { _busy = false; }
    }
}
