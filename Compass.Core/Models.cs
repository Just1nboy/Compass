using System.Globalization;

namespace Compass.Models;

/// <summary>
/// Anything that syncs item-by-item across devices. Each item carries its own
/// last-changed clock and a tombstone flag, so a merge can combine both devices'
/// changes instead of one device's whole snapshot overwriting the other's.
/// </summary>
public interface ISyncable
{
    string Id { get; }
    DateTime UpdatedUtc { get; set; }
    bool Deleted { get; set; }
}

/// <summary>Helpers to stamp/soft-delete syncable items so merges resolve correctly.</summary>
public static class Syncable
{
    /// <summary>Mark an item as changed "now" so this edit wins the next merge.</summary>
    public static T Touch<T>(this T item) where T : ISyncable
    {
        item.UpdatedUtc = DateTime.UtcNow;
        return item;
    }

    /// <summary>Soft-delete: keep a tombstone so the deletion propagates and the item never resurrects.</summary>
    public static void MarkDeleted(this ISyncable item)
    {
        item.Deleted = true;
        item.UpdatedUtc = DateTime.UtcNow;
    }
}

/// <summary>A tracked deadline / event (exam, assignment, admin task, etc.).</summary>
public class Deadline : ISyncable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public DateTime Due { get; set; }
    public string Kind { get; set; } = "Deadline"; // Exam, Assignment, Admin, Other, Deadline
    public bool Critical { get; set; }
    public string Notes { get; set; } = "";
    public string WhereToFind { get; set; } = ""; // the "where's the answer / who to ask" field
    public bool Completed { get; set; }
    public bool Acknowledged { get; set; } // for must-acknowledge criticals
    public int PrepDays { get; set; } = 0;  // start-preparing reminder, 0 = none
    public List<string> Fired { get; set; } = new(); // reminder thresholds already fired
    public DateTime? LastCriticalNudge { get; set; }

    // ---- Sync bookkeeping ----
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool Deleted { get; set; }
}

public class PlaybookStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public string Ask { get; set; } = "";      // the question you should ask
    public string WhereToFind { get; set; } = ""; // where to get the answer
    public bool Done { get; set; }
}

public class Playbook : ISyncable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<PlaybookStep> Steps { get; set; } = new();

    // A playbook syncs as a whole (steps included); toggling a step touches the playbook.
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool Deleted { get; set; }
}

/// <summary>A deadline candidate extracted from an email by the AI, awaiting the user's confirmation.</summary>
public class ExtractedDeadline
{
    public string Title { get; set; } = "";
    public string DueIso { get; set; } = "";
    public string Kind { get; set; } = "Deadline";
    public bool Critical { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = "";
    public string Account { get; set; } = "";

    public DateTime? Due =>
        DateTime.TryParse(DueIso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>An important, actionable thing found in an email that has no hard date.</summary>
public class EmailAction
{
    public string Text { get; set; } = "";     // what you need to do / know
    public string Source { get; set; } = "";    // the email subject it came from
    public string Account { get; set; } = "";
}

/// <summary>The full result of an email scan: dated deadlines + undated action items.</summary>
public class EmailScan
{
    public List<ExtractedDeadline> Deadlines { get; set; } = new();
    public List<EmailAction> Actions { get; set; } = new();
    public int EmailCount { get; set; }
    public bool IsEmpty => Deadlines.Count == 0 && Actions.Count == 0;
}

public class CaptureItem : ISyncable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool Processed { get; set; }

    // ---- Sync bookkeeping ----
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool Deleted { get; set; }
}

public class EmailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "";       // "Gmail", "BINUS"
    public string Email { get; set; } = "";
    public string Provider { get; set; } = "imap"; // "gmail" | "microsoft" | "imap"
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public string AuthType { get; set; } = "password"; // "password" | "oauth"
    public string SecretEnc { get; set; } = "";    // DPAPI-encrypted app password
    public string OAuthClientId { get; set; } = ""; // Microsoft app (client) id
    public bool Enabled { get; set; } = true;
    public string LastStatus { get; set; } = "";    // last connection result, for the UI
}

public class Settings
{
    public string ClaudeModel { get; set; } = "sonnet";
    public string ClaudePath { get; set; } = "";   // optional override to claude.cmd
    public int ImportDays { get; set; } = 14;       // how far back to scan email
    public DateTime? LastImport { get; set; }
    public bool AccountsSeeded { get; set; }
    public List<EmailAccount> Accounts { get; set; } = new();

    // ---- Cloud sync (Supabase) ----
    public string SyncEmail { get; set; } = "";
    public string SyncUserId { get; set; } = "";
    public string SyncRefreshToken { get; set; } = "";
    public string SyncAccessToken { get; set; } = "";
    public DateTime SyncAccessExpiryUtc { get; set; }
    public DateTime? LastSyncUtc { get; set; }
}

public class AppData
{
    public List<Deadline> Deadlines { get; set; } = new();
    public List<Playbook> Playbooks { get; set; } = new();
    public List<CaptureItem> Inbox { get; set; } = new();
    public Settings Settings { get; set; } = new();
    public bool Seeded { get; set; }

    // Bumped to UtcNow on every local change; used by sync as a last-write-wins clock.
    public DateTime DataUpdatedUtc { get; set; }
}

/// <summary>Human-friendly formatting designed to be hard to misread.</summary>
public static class Humanize
{
    public static string PartOfDay(int hour) => hour switch
    {
        >= 5 and < 12 => "morning",
        >= 12 and < 17 => "afternoon",
        >= 17 and < 21 => "evening",
        _ => "night"
    };

    /// <summary>e.g. "Wednesday, 15 July 2026 at 9:00 AM (morning)"</summary>
    public static string ReadBack(DateTime dt)
    {
        string s = dt.ToString("dddd, d MMMM yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
        return $"{s}  ({PartOfDay(dt.Hour)})";
    }

    /// <summary>Relative, clear countdown that is hard to misread.</summary>
    public static string Countdown(DateTime due, DateTime now)
    {
        TimeSpan span = due - now;
        if (span.TotalSeconds < 0)
        {
            TimeSpan od = now - due;
            if (od.TotalDays >= 1) return $"OVERDUE by {(int)od.TotalDays} day(s)";
            if (od.TotalHours >= 1) return $"OVERDUE by {(int)od.TotalHours}h";
            return $"OVERDUE by {(int)od.TotalMinutes} min";
        }

        if (due.Date == now.Date)
        {
            if (span.TotalHours >= 1) return $"TODAY · in {(int)span.TotalHours}h {span.Minutes}m";
            return $"TODAY · in {(int)span.TotalMinutes} min";
        }
        if (due.Date == now.Date.AddDays(1))
            return $"Tomorrow · {due:h:mm tt}";

        int days = (int)(due.Date - now.Date).TotalDays;
        return $"in {days} days · {due:ddd d MMM}";
    }

    /// <summary>Urgency bucket used for colour-coding.</summary>
    public static string Urgency(DateTime due, DateTime now, bool completed)
    {
        if (completed) return "done";
        TimeSpan span = due - now;
        if (span.TotalSeconds < 0) return "overdue";
        if (span.TotalHours <= 24) return "urgent";
        if (span.TotalDays <= 3) return "soon";
        if (span.TotalDays <= 7) return "upcoming";
        return "normal";
    }
}
