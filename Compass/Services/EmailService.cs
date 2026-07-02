using Compass.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Identity.Client;

namespace Compass.Services;

public sealed class EmailMsg
{
    public string Subject { get; set; } = "";
    public string From { get; set; } = "";
    public DateTime DateSent { get; set; }
    public string Preview { get; set; } = "";
    public string AccountLabel { get; set; } = "";
}

/// <summary>Reads recent mail over IMAP (Gmail via app password, Microsoft 365 via OAuth).</summary>
public sealed class EmailService
{
    private async Task AuthenticateAsync(ImapClient client, EmailAccount acct,
        Func<DeviceCodeResult, Task>? onDeviceCode, CancellationToken ct)
    {
        if (acct.AuthType == "oauth")
        {
            string token = await new MicrosoftAuth()
                .GetTokenAsync(acct, onDeviceCode ?? (_ => Task.CompletedTask), ct);
            var oauth = new SaslMechanismOAuth2(acct.Email, token);
            await client.AuthenticateAsync(oauth, ct);
        }
        else
        {
            string pass = Crypto.Unprotect(acct.SecretEnc);
            if (string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("No app password saved for this account.");
            await client.AuthenticateAsync(acct.Email, pass, ct);
        }
    }

    /// <summary>Connect + authenticate only, to verify the account works.</summary>
    public async Task<string> TestAsync(EmailAccount acct, Func<DeviceCodeResult, Task>? onDeviceCode,
        CancellationToken ct = default)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(acct.ImapHost, acct.ImapPort, SecureSocketOptions.SslOnConnect, ct);
        await AuthenticateAsync(client, acct, onDeviceCode, ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
        int count = inbox.Count;
        await client.DisconnectAsync(true, ct);
        return $"Connected — inbox has {count} messages.";
    }

    public async Task<List<EmailMsg>> FetchAsync(EmailAccount acct, int days,
        Func<DeviceCodeResult, Task>? onDeviceCode, CancellationToken ct = default)
    {
        var result = new List<EmailMsg>();
        using var client = new ImapClient();
        await client.ConnectAsync(acct.ImapHost, acct.ImapPort, SecureSocketOptions.SslOnConnect, ct);
        await AuthenticateAsync(client, acct, onDeviceCode, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        DateTime since = DateTime.Now.AddDays(-Math.Max(1, days));
        var uids = await inbox.SearchAsync(SearchQuery.DeliveredAfter(since), ct);

        // Bound the work: newest ~60 messages, envelope + a short preview (no full download).
        var pick = uids.Reverse().Take(60).ToList();
        if (pick.Count > 0)
        {
            var summaries = await inbox.FetchAsync(pick,
                MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText, ct);

            foreach (var s in summaries)
            {
                result.Add(new EmailMsg
                {
                    Subject = s.Envelope?.Subject ?? "(no subject)",
                    From = s.Envelope?.From?.ToString() ?? "",
                    DateSent = s.Envelope?.Date?.LocalDateTime ?? DateTime.Now,
                    Preview = (s.PreviewText ?? "").Replace("\r", " ").Replace("\n", " ").Trim(),
                    AccountLabel = acct.Label,
                });
            }
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }
}
