using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Compass.Services;

/// <summary>
/// Talks to Claude through the local Claude Code CLI in headless mode
/// (`claude -p --output-format json`). This uses the user's logged-in
/// subscription — no API key required.
/// </summary>
public sealed class ClaudeService
{
    public static string ResolveClaude()
    {
        string overridePath = DataStore.Instance.Data.Settings.ClaudePath;
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string cmd = Path.Combine(appData, "npm", "claude.cmd");
        if (File.Exists(cmd)) return cmd;

        return "claude.cmd"; // fall back to PATH
    }

    public static bool IsAvailable()
    {
        string p = ResolveClaude();
        return p == "claude.cmd" || File.Exists(p);
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        string model = DataStore.Instance.Data.Settings.ClaudeModel;
        if (string.IsNullOrWhiteSpace(model)) model = "sonnet";

        string claude = ResolveClaude();

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = DataStore.DataFolder,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(claude);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);
        psi.ArgumentList.Add("--disallowed-tools");
        psi.ArgumentList.Add("Bash,Edit,Write,Read,WebSearch,WebFetch,Glob,Grep,Task,NotebookEdit,TodoWrite");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(180));

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Couldn't launch the Claude Code CLI. Make sure `claude` is installed and you're logged in. " + ex.Message);
        }

        await proc.StandardInput.WriteAsync(prompt.AsMemory(), timeout.Token);
        proc.StandardInput.Close();

        Task<string> outTask = proc.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errTask = proc.StandardError.ReadToEndAsync(timeout.Token);
        await Task.WhenAll(outTask, errTask);
        await proc.WaitForExitAsync(timeout.Token);

        string stdout = outTask.Result;
        string stderr = errTask.Result;

        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException("No response from Claude. " + Trim(stderr));

        try
        {
            using JsonDocument doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (root.TryGetProperty("is_error", out var err) && err.GetBoolean())
                throw new InvalidOperationException("Claude returned an error: " +
                    (root.TryGetProperty("result", out var r) ? r.GetString() : Trim(stderr)));

            return root.TryGetProperty("result", out var res) ? res.GetString() ?? "" : stdout;
        }
        catch (JsonException)
        {
            // Not the wrapper JSON we expected — return whatever came back.
            return stdout;
        }
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] : s;

    /// <summary>Pull a JSON object/array out of model text that may contain prose or ``` fences.</summary>
    public static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string t = text.Trim();

        int fence = t.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            int nl = t.IndexOf('\n', fence);
            int close = t.IndexOf("```", fence + 3, StringComparison.Ordinal);
            if (nl >= 0 && close > nl)
                t = t.Substring(nl + 1, close - nl - 1).Trim();
        }

        int start = t.IndexOfAny(new[] { '{', '[' });
        int end = t.LastIndexOfAny(new[] { '}', ']' });
        if (start >= 0 && end > start)
            return t.Substring(start, end - start + 1);

        return t;
    }
}
