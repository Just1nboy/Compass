using Compass.Models;
using Microsoft.Identity.Client;

namespace Compass.Services;

/// <summary>Fetches recent mail from all enabled accounts and runs the AI scan. Shared by the
/// manual "Import now" button and the automatic background scan.</summary>
public sealed class EmailImporter
{
    public async Task<(EmailScan scan, List<string> errors)> RunAsync(
        int days, Func<DeviceCodeResult, Task>? onDeviceCode, Action<string>? status, CancellationToken ct = default)
    {
        var settings = DataStore.Instance.Data.Settings;
        var emails = new List<EmailMsg>();
        var errors = new List<string>();

        foreach (var acct in settings.Accounts.Where(a => a.Enabled))
        {
            status?.Invoke($"Reading {acct.Label}…");
            try { emails.AddRange(await new EmailService().FetchAsync(acct, days, onDeviceCode, ct)); }
            catch (Exception ex) { errors.Add($"{acct.Label}: {ex.Message}"); }
        }

        if (emails.Count == 0) return (new EmailScan(), errors);

        status?.Invoke($"Reading {emails.Count} emails with AI…");
        var scan = await new AiTasks().ScanEmailsAsync(emails, ct);

        foreach (var d in scan.Deadlines) d.Account = MatchAccount(emails, d.Source);
        foreach (var a in scan.Actions) a.Account = MatchAccount(emails, a.Source);

        return (scan, errors);
    }

    private static string MatchAccount(List<EmailMsg> emails, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        var m = emails.FirstOrDefault(e =>
            e.Subject.Contains(source, StringComparison.OrdinalIgnoreCase) ||
            source.Contains(e.Subject, StringComparison.OrdinalIgnoreCase));
        return m?.AccountLabel ?? "";
    }

    /// <summary>Add action items to the capture inbox, skipping duplicates. Returns how many were added.</summary>
    public static int AddActionsToInbox(IEnumerable<EmailAction> actions)
    {
        var inbox = DataStore.Instance.Data.Inbox;
        int added = 0;
        foreach (var a in actions)
        {
            string text = a.Text.Trim();
            if (text.Length == 0) continue;
            if (!string.IsNullOrWhiteSpace(a.Source)) text += $"  (email: {a.Source})";
            if (inbox.Any(i => !i.Deleted && string.Equals(i.Text, text, StringComparison.OrdinalIgnoreCase))) continue;
            inbox.Insert(0, new CaptureItem { Text = text });
            added++;
        }
        if (added > 0) DataStore.Instance.Save();
        return added;
    }
}
