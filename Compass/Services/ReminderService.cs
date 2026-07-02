using System.Windows.Threading;
using Compass.Models;
using WF = System.Windows.Forms;

namespace Compass.Services;

/// <summary>
/// The always-on background engine. Runs on a timer, fires escalating reminders,
/// and keeps nudging critical items until they are acknowledged.
/// </summary>
public sealed class ReminderService
{
    private readonly WF.NotifyIcon _tray;
    private readonly DispatcherTimer _timer;

    public ReminderService(WF.NotifyIcon tray)
    {
        _tray = tray;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        Tick();          // run once immediately
        _timer.Start();
    }

    private void Tick()
    {
        DateTime now = DateTime.Now;
        bool changed = false;

        foreach (Deadline d in DataStore.Instance.Data.Deadlines)
        {
            if (d.Completed || d.Deleted) continue;

            foreach ((string key, DateTime when, string msg) in Thresholds(d))
            {
                if (d.Fired.Contains(key)) continue;
                if (when > now) continue;

                // Fire only if it just crossed (within 12h); otherwise mark silently
                // so adding a last-minute deadline doesn't dump a week of old reminders.
                if (when > now.AddHours(-12))
                    Notify(TitleFor(d), msg);

                d.Fired.Add(key);
                changed = true;
            }

            // Must-acknowledge: keep nudging criticals in the final 24h until acknowledged.
            if (d.Critical && !d.Acknowledged &&
                now >= d.Due.AddHours(-24) && now <= d.Due.AddHours(6))
            {
                if (d.LastCriticalNudge == null ||
                    (now - d.LastCriticalNudge.Value).TotalMinutes >= 15)
                {
                    Notify($"⚠ ACTION NEEDED — {d.Title}",
                        $"{Humanize.Countdown(d.Due, now)}. {Humanize.ReadBack(d.Due)}. " +
                        "Open Compass and tap ‘I've got this’ to stop these alerts.");
                    d.LastCriticalNudge = now;
                    changed = true;
                }
            }
        }

        if (changed) DataStore.Instance.Save();
    }

    private static string TitleFor(Deadline d) => d.Kind switch
    {
        "Exam" => "📘 Exam reminder",
        "Assignment" => "📝 Assignment due",
        "Admin" => "📋 Admin task",
        _ => "⏰ Reminder"
    };

    private static IEnumerable<(string key, DateTime when, string msg)> Thresholds(Deadline d)
    {
        var list = new List<(string, DateTime, string)>
        {
            ("7d",   d.Due.AddDays(-7), $"1 week until “{d.Title}”\n{Humanize.ReadBack(d.Due)}"),
            ("3d",   d.Due.AddDays(-3), $"3 days until “{d.Title}”\n{Humanize.ReadBack(d.Due)}"),
            ("1d",   d.Due.AddDays(-1), $"Tomorrow: “{d.Title}”\n{Humanize.ReadBack(d.Due)}"),
            ("morn", d.Due.Date.AddHours(7), $"TODAY: “{d.Title}” at {d.Due:h:mm tt} ({Humanize.PartOfDay(d.Due.Hour)}). Double-check the time now."),
            ("2h",   d.Due.AddHours(-2), $"In ~2 hours: “{d.Title}” at {d.Due:h:mm tt}. Get ready now."),
        };

        if (d.PrepDays > 0)
            list.Add(("prep", d.Due.AddDays(-d.PrepDays),
                $"Time to start preparing for “{d.Title}” — it's {d.PrepDays} day(s) away ({d.Due:ddd d MMM})."));

        // Only thresholds that fall before the due moment make sense.
        return list.Where(x => x.Item2 < d.Due.AddMinutes(1));
    }

    private void Notify(string title, string message)
    {
        try
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = message;
            _tray.ShowBalloonTip(20000, title, message, WF.ToolTipIcon.Info);
        }
        catch { }
    }
}
