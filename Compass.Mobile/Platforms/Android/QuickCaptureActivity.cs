using System.Text.Json;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Compass.Models;

namespace Compass.Mobile;

// A tiny dialog-styled screen the widget opens. Type a note, Save, done — the full app never opens.
// Writes straight to the same data.json the app uses (Context.FilesDir == MAUI AppDataDirectory).
[Activity(
    Label = "Quick capture",
    Theme = "@android:style/Theme.Material.Dialog.NoActionBar",
    Exported = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class QuickCaptureActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.quick_capture_activity);

        // The dialog theme shrink-wraps its content, which left the box narrow.
        // Stretch the window to almost the full screen width so there's room to type.
        if (Window is not null)
        {
            int width = (int)(Resources!.DisplayMetrics!.WidthPixels * 0.94);
            Window.SetLayout(width, ViewGroup.LayoutParams.WrapContent);
        }

        var input = FindViewById<EditText>(Resource.Id.captureInput)!;
        var save = FindViewById<Android.Widget.Button>(Resource.Id.saveBtn)!;
        var cancel = FindViewById<Android.Widget.Button>(Resource.Id.cancelBtn)!;

        Window?.SetSoftInputMode(SoftInput.StateVisible);
        input.RequestFocus();

        cancel.Click += (_, _) => Finish();
        save.Click += (_, _) =>
        {
            string text = (input.Text ?? "").Trim();
            if (text.Length == 0) { Finish(); return; }
            try
            {
                AppendToInbox(text);
                Toast.MakeText(this, "Saved to Compass ✓", ToastLength.Short)?.Show();
            }
            catch
            {
                Toast.MakeText(this, "Couldn't save — try again", ToastLength.Short)?.Show();
            }
            Finish();
        };
    }

    private void AppendToInbox(string text)
    {
        string dir = ApplicationContext!.FilesDir!.AbsolutePath;
        string file = System.IO.Path.Combine(dir, "data.json");

        var opts = new JsonSerializerOptions { WriteIndented = true };
        AppData data = new();
        try
        {
            if (System.IO.File.Exists(file))
                data = JsonSerializer.Deserialize<AppData>(System.IO.File.ReadAllText(file)) ?? new AppData();
        }
        catch { data = new AppData(); }

        data.Inbox.Insert(0, new CaptureItem { Text = text });
        data.DataUpdatedUtc = DateTime.UtcNow;   // so the next sync pushes it to the PC
        System.IO.File.WriteAllText(file, JsonSerializer.Serialize(data, opts));
    }
}
