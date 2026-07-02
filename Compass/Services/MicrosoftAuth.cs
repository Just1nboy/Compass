using System.IO;
using Compass.Models;
using Microsoft.Identity.Client;

namespace Compass.Services;

/// <summary>
/// Microsoft 365 sign-in for BINUS mail, using MSAL's device-code flow so we never
/// handle the password. Tokens are cached encrypted per account for silent refresh.
/// Requires a one-time Azure app registration (public client, IMAP.AccessAsUser.All).
/// </summary>
public sealed class MicrosoftAuth
{
    // Outlook IMAP scope + refresh.
    private static readonly string[] Scopes =
    {
        "https://outlook.office365.com/IMAP.AccessAsUser.All",
        "offline_access"
    };

    private static string CacheFile(string acctId) => Path.Combine(DataStore.DataFolder, $"msal_{acctId}.bin");

    private static IPublicClientApplication Build(EmailAccount acct)
    {
        var app = PublicClientApplicationBuilder
            .Create(acct.OAuthClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "organizations")
            .Build();

        app.UserTokenCache.SetBeforeAccess(args =>
        {
            try
            {
                string f = CacheFile(acct.Id);
                if (!File.Exists(f)) return;
                string b64 = Crypto.Unprotect(File.ReadAllText(f));
                if (!string.IsNullOrEmpty(b64))
                    args.TokenCache.DeserializeMsalV3(Convert.FromBase64String(b64));
            }
            catch { }
        });

        app.UserTokenCache.SetAfterAccess(args =>
        {
            if (!args.HasStateChanged) return;
            try
            {
                byte[] bytes = args.TokenCache.SerializeMsalV3();
                File.WriteAllText(CacheFile(acct.Id), Crypto.Protect(Convert.ToBase64String(bytes)));
            }
            catch { }
        });

        return app;
    }

    /// <summary>Get an access token, silently if possible, otherwise via device code.</summary>
    public async Task<string> GetTokenAsync(EmailAccount acct, Func<DeviceCodeResult, Task> onDeviceCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(acct.OAuthClientId))
            throw new InvalidOperationException(
                "BINUS needs a Microsoft app (client) ID first — see the setup steps in Settings.");

        var app = Build(acct);
        var accounts = await app.GetAccountsAsync();
        try
        {
            var silent = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync(ct);
            return silent.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            var result = await app.AcquireTokenWithDeviceCode(Scopes, onDeviceCode).ExecuteAsync(ct);
            return result.AccessToken;
        }
    }

    public static void ClearCache(string acctId)
    {
        try { if (File.Exists(CacheFile(acctId))) File.Delete(CacheFile(acctId)); } catch { }
    }
}
