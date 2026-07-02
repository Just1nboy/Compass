using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace Compass.Mobile;

// A home-screen widget. Tapping it opens the lightweight QuickCaptureActivity popup
// (Android widgets can't host a live text field, so tap-to-capture is the pattern).
[BroadcastReceiver(Label = "Compass Quick Capture", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/quickcapture_widget_info")]
public class QuickCaptureWidget : AppWidgetProvider
{
    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        foreach (int id in appWidgetIds)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.quickcapture_widget);

            var intent = new Intent(context, typeof(QuickCaptureActivity));
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

            var pending = PendingIntent.GetActivity(context, 0, intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            views.SetOnClickPendingIntent(Resource.Id.widget_root, pending);
            appWidgetManager.UpdateAppWidget(id, views);
        }
    }
}
