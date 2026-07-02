using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Compass.Models;

namespace Compass.Sync;

public enum SyncOutcome { NotSignedIn, PushedLocal, AdoptedRemote, NoChange, Error }

public readonly record struct SyncResult(SyncOutcome Outcome, string Message);

/// <summary>
/// Two-way sync of deadlines/playbooks/inbox through Supabase, over plain REST.
/// Auth is email+password (GoTrue); data is a single per-user JSON blob with
/// last-write-wins on <see cref="AppData.DataUpdatedUtc"/>. No SDK dependency.
/// </summary>
public sealed class SyncService
{
    public static SyncService Instance { get; } = new();

    private const string BaseUrl = "https://kqjmflourqabbftmykur.supabase.co";
    private const string ApiKey = "sb_publishable_ZBfGOvxAEZqfYSrfMYkbIg_bKaN30Fh";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    public bool IsSignedIn(Settings s) => !string.IsNullOrEmpty(s.SyncRefreshToken);

    // ---------------- Auth ----------------

    public async Task<string> SignUpAsync(string email, string password, Settings s, CancellationToken ct = default)
    {
        var resp = await PostAuthAsync("/auth/v1/signup", new PasswordBody { Email = email, Password = password }, ct);
        if (string.IsNullOrEmpty(resp.AccessToken))
            return "Account created. If Supabase requires email confirmation, confirm it, then tap Sign in.";
        StoreSession(resp, email, s);
        return "Account created and signed in as " + email + ".";
    }

    public async Task<string> SignInAsync(string email, string password, Settings s, CancellationToken ct = default)
    {
        var resp = await PostAuthAsync("/auth/v1/token?grant_type=password",
            new PasswordBody { Email = email, Password = password }, ct);
        if (string.IsNullOrEmpty(resp.AccessToken))
            throw new InvalidOperationException("Sign in failed — check your email and password.");
        StoreSession(resp, email, s);
        return "Signed in as " + email + ".";
    }

    public void SignOut(Settings s)
    {
        s.SyncEmail = s.SyncUserId = s.SyncRefreshToken = s.SyncAccessToken = "";
        s.SyncAccessExpiryUtc = default;
    }

    private void StoreSession(AuthResponse r, string email, Settings s)
    {
        s.SyncEmail = email;
        s.SyncUserId = r.User?.Id ?? s.SyncUserId;
        s.SyncRefreshToken = r.RefreshToken ?? "";
        s.SyncAccessToken = r.AccessToken ?? "";
        s.SyncAccessExpiryUtc = DateTime.UtcNow.AddSeconds(r.ExpiresIn > 0 ? r.ExpiresIn : 3600);
    }

    private async Task<AuthResponse> PostAuthAsync(string path, object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
        req.Headers.Add("apikey", ApiKey);
        req.Content = JsonContent.Create(body);
        using var res = await Http.SendAsync(req, ct);
        string txt = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(FriendlyAuthError(txt));
        return JsonSerializer.Deserialize<AuthResponse>(txt, J) ?? new AuthResponse();
    }

    private async Task<string> EnsureAccessTokenAsync(Settings s, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(s.SyncAccessToken) && DateTime.UtcNow < s.SyncAccessExpiryUtc.AddSeconds(-60))
            return s.SyncAccessToken;
        if (string.IsNullOrEmpty(s.SyncRefreshToken))
            throw new InvalidOperationException("Not signed in.");

        var resp = await PostAuthAsync("/auth/v1/token?grant_type=refresh_token",
            new RefreshBody { RefreshToken = s.SyncRefreshToken }, ct);
        if (string.IsNullOrEmpty(resp.AccessToken))
            throw new InvalidOperationException("Session expired — please sign in again.");
        StoreSession(resp, s.SyncEmail, s);
        return resp.AccessToken!;
    }

    // ---------------- Sync ----------------

    // Tombstones older than this are dropped once both sides have converged. Must comfortably
    // exceed how long a device might stay offline, or a stale device could resurrect a deletion.
    private static readonly TimeSpan TombstoneTtl = TimeSpan.FromDays(60);

    public async Task<SyncResult> SyncAsync(AppData local, CancellationToken ct = default)
    {
        Settings s = local.Settings;
        if (!IsSignedIn(s)) return new SyncResult(SyncOutcome.NotSignedIn, "Not signed in.");

        string token;
        try { token = await EnsureAccessTokenAsync(s, ct); }
        catch (Exception ex) { return new SyncResult(SyncOutcome.Error, ex.Message); }

        try
        {
            var remote = await PullAsync(s.SyncUserId, token, ct);

            if (remote == null)
            {
                // Nothing in the cloud yet — seed it with what this device has.
                await PushAsync(s.SyncUserId, token, local, ct);
                s.LastSyncUtc = DateTime.UtcNow;
                return new SyncResult(SyncOutcome.PushedLocal, "Saved to the cloud.");
            }

            var rp = remote.Value.Payload;
            var remoteDeadlines = rp.Deadlines ?? new List<Deadline>();
            var remotePlaybooks = rp.Playbooks ?? new List<Playbook>();
            var remoteInbox = rp.Inbox ?? new List<CaptureItem>();

            // Per-item union: combine both devices, newest edit wins each item, tombstones
            // carry deletions across. Nothing that exists on only one side is ever dropped.
            var mergedDeadlines = Purge(MergeList(local.Deadlines, remoteDeadlines));
            var mergedPlaybooks = Purge(MergeList(local.Playbooks, remotePlaybooks));
            var mergedInbox = Purge(MergeList(local.Inbox, remoteInbox));

            bool localChanged =
                !SameSet(local.Deadlines, mergedDeadlines) ||
                !SameSet(local.Playbooks, mergedPlaybooks) ||
                !SameSet(local.Inbox, mergedInbox);

            bool remoteStale =
                !SameSet(remoteDeadlines, mergedDeadlines) ||
                !SameSet(remotePlaybooks, mergedPlaybooks) ||
                !SameSet(remoteInbox, mergedInbox);

            local.Deadlines = mergedDeadlines;
            local.Playbooks = mergedPlaybooks;
            local.Inbox = mergedInbox;
            local.DataUpdatedUtc = MaxStamp(local);

            if (remoteStale)
                await PushAsync(s.SyncUserId, token, local, ct);

            s.LastSyncUtc = DateTime.UtcNow;

            // AdoptedRemote whenever our local set changed, so the UI knows to refresh.
            if (localChanged) return new SyncResult(SyncOutcome.AdoptedRemote, "Synced — both devices merged.");
            if (remoteStale) return new SyncResult(SyncOutcome.PushedLocal, "Saved to the cloud.");
            return new SyncResult(SyncOutcome.NoChange, "Already up to date.");
        }
        catch (Exception ex)
        {
            return new SyncResult(SyncOutcome.Error, ex.Message);
        }
    }

    /// <summary>Union two item lists by Id; on a conflict the item with the newer clock wins.</summary>
    private static List<T> MergeList<T>(List<T> local, List<T> remote) where T : ISyncable
    {
        var byId = new Dictionary<string, T>();
        foreach (var it in remote) byId[it.Id] = it;
        foreach (var it in local)
        {
            if (byId.TryGetValue(it.Id, out var other))
                byId[it.Id] = it.UpdatedUtc >= other.UpdatedUtc ? it : other; // tie → keep local
            else
                byId[it.Id] = it;
        }
        return byId.Values.ToList();
    }

    /// <summary>Drop tombstones old enough that every device has surely seen the deletion.</summary>
    private static List<T> Purge<T>(List<T> items) where T : ISyncable
    {
        DateTime cutoff = DateTime.UtcNow - TombstoneTtl;
        return items.Where(i => !(i.Deleted && i.UpdatedUtc < cutoff)).ToList();
    }

    /// <summary>True if the two lists hold the same items in the same version (id + clock + tombstone).</summary>
    private static bool SameSet<T>(List<T> a, List<T> b) where T : ISyncable
    {
        if (a.Count != b.Count) return false;
        var mb = b.ToDictionary(x => x.Id);
        foreach (var x in a)
        {
            if (!mb.TryGetValue(x.Id, out var y)) return false;
            if (x.UpdatedUtc != y.UpdatedUtc || x.Deleted != y.Deleted) return false;
        }
        return true;
    }

    private static DateTime MaxStamp(AppData d)
    {
        DateTime max = d.DataUpdatedUtc;
        foreach (var x in d.Deadlines) if (x.UpdatedUtc > max) max = x.UpdatedUtc;
        foreach (var x in d.Playbooks) if (x.UpdatedUtc > max) max = x.UpdatedUtc;
        foreach (var x in d.Inbox) if (x.UpdatedUtc > max) max = x.UpdatedUtc;
        return max;
    }

    private async Task<(SyncPayload Payload, DateTime UpdatedUtc)?> PullAsync(string uid, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/rest/v1/app_state?id=eq.{uid}&select=data,updated_at");
        req.Headers.Add("apikey", ApiKey);
        req.Headers.Add("Authorization", "Bearer " + token);
        using var res = await Http.SendAsync(req, ct);
        string txt = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(FriendlyRestError(txt));

        using var doc = JsonDocument.Parse(txt);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        var row = doc.RootElement[0];
        string data = row.GetProperty("data").GetString() ?? "{}";
        DateTime upd = DateTimeOffset.Parse(row.GetProperty("updated_at").GetString()!).UtcDateTime;
        var payload = JsonSerializer.Deserialize<SyncPayload>(data, J) ?? new SyncPayload();
        return (payload, upd);
    }

    private async Task PushAsync(string uid, string token, AppData local, CancellationToken ct)
    {
        var payload = new SyncPayload
        {
            Deadlines = local.Deadlines,
            Playbooks = local.Playbooks,
            Inbox = local.Inbox,
        };
        var row = new RowDto
        {
            Id = uid,
            Data = JsonSerializer.Serialize(payload),
            UpdatedAt = local.DataUpdatedUtc.ToString("o"),
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/app_state");
        req.Headers.Add("apikey", ApiKey);
        req.Headers.Add("Authorization", "Bearer " + token);
        req.Headers.Add("Prefer", "resolution=merge-duplicates");
        req.Content = JsonContent.Create(new[] { row });
        using var res = await Http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(FriendlyRestError(await res.Content.ReadAsStringAsync(ct)));
    }

    private static string FriendlyAuthError(string body)
    {
        try
        {
            using var d = JsonDocument.Parse(body);
            foreach (var key in new[] { "msg", "error_description", "message", "error" })
                if (d.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString()!;
        }
        catch { }
        return body.Length > 200 ? body[..200] : body;
    }

    private static string FriendlyRestError(string body)
    {
        if (body.Contains("does not exist") || body.Contains("Could not find the table") || body.Contains("relation"))
            return "The cloud table isn't set up yet — run the one-time setup SQL in Supabase.";
        try
        {
            using var d = JsonDocument.Parse(body);
            if (d.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString()!;
        }
        catch { }
        return body.Length > 200 ? body[..200] : body;
    }

    // ---- DTOs ----
    private sealed class PasswordBody
    {
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("password")] public string Password { get; set; } = "";
    }
    private sealed class RefreshBody
    {
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
    }
    private sealed class RowDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("data")] public string Data { get; set; } = "";
        [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
    }
    private sealed class AuthResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("user")] public AuthUser? User { get; set; }
    }
    private sealed class AuthUser
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    public sealed class SyncPayload
    {
        public List<Deadline>? Deadlines { get; set; }
        public List<Playbook>? Playbooks { get; set; }
        public List<CaptureItem>? Inbox { get; set; }
    }
}
