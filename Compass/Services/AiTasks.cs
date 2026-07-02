using System.Text;
using System.Text.Json;
using Compass.Models;

namespace Compass.Services;

/// <summary>The two AI features, expressed as prompts to <see cref="ClaudeService"/>.</summary>
public sealed class AiTasks
{
    private readonly ClaudeService _claude = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>"What am I missing?" — turn a plain-language situation into a concrete checklist.</summary>
    public async Task<Playbook> GenerateChecklistAsync(string situation, CancellationToken ct = default)
    {
        string prompt =
            "You are helping a university student who struggles with executive function and, in his own words, " +
            "\"doesn't know what he doesn't know\" — he misses things because he never realised he had to ask. " +
            "Given his situation below, produce a concrete, ordered checklist of what he needs to do. " +
            "For EACH step include three things: the action to take, the exact QUESTION he should ask (so he stops " +
            "getting blindsided), and WHERE/WHO to get the answer from. Be specific and practical, not generic. " +
            "Assume nothing is obvious to him.\n\n" +
            "Output ONLY valid JSON, no prose, in exactly this shape:\n" +
            "{\"title\":\"short title\",\"description\":\"one sentence\",\"steps\":[{\"text\":\"the action\"," +
            "\"ask\":\"the question to ask\",\"where\":\"where/who to find the answer\"}]}\n\n" +
            "His situation:\n" + situation;

        string raw = await _claude.CompleteAsync(prompt, ct);
        string json = ClaudeService.ExtractJson(raw);

        var dto = JsonSerializer.Deserialize<ChecklistDto>(json, JsonOpts)
                  ?? throw new InvalidOperationException("Claude didn't return a usable checklist.");

        var pb = new Playbook
        {
            Title = string.IsNullOrWhiteSpace(dto.Title) ? "My checklist" : dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? "",
        };
        foreach (var s in dto.Steps ?? new())
        {
            if (string.IsNullOrWhiteSpace(s.Text)) continue;
            pb.Steps.Add(new PlaybookStep
            {
                Text = s.Text.Trim(),
                Ask = s.Ask?.Trim() ?? "",
                WhereToFind = s.Where?.Trim() ?? "",
            });
        }
        if (pb.Steps.Count == 0)
            throw new InvalidOperationException("Claude didn't return any steps. Try rephrasing the situation.");

        return pb;
    }

    /// <summary>Scan emails for BOTH dated deadlines AND undated action items worth handling.</summary>
    public async Task<EmailScan> ScanEmailsAsync(IReadOnlyList<EmailMsg> emails, CancellationToken ct = default)
    {
        var scan = new EmailScan { EmailCount = emails.Count };
        if (emails.Count == 0) return scan;

        var sb = new StringBuilder();
        int i = 1;
        foreach (var e in emails)
        {
            sb.AppendLine($"--- Email {i++} (account: {e.AccountLabel}) ---");
            sb.AppendLine($"From: {e.From}");
            sb.AppendLine($"Received: {e.DateSent:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Subject: {e.Subject}");
            sb.AppendLine($"Body: {e.Preview}");
            sb.AppendLine();
        }

        string prompt =
            $"Today is {DateTime.Now:dddd, yyyy-MM-dd HH:mm}. You are a university student's assistant scanning his recent " +
            "emails. He misses things easily, so pull out anything that matters. Return TWO lists:\n\n" +
            "1) DEADLINES — anything with a real date/time: exam, due date, appointment, submission cut-off, payment due, " +
            "registration window. Resolve the actual calendar date (interpret 'next Friday' etc. relative to today). If no " +
            "time is given use 09:00 and lower confidence. kind = Exam | Assignment | Admin | Other; critical=true for exams " +
            "and hard submission deadlines.\n\n" +
            "2) ACTIONS — important things he needs to DO or KNOW that have NO hard date: a form or document to submit, a " +
            "reply someone is waiting for, a fee mentioned, an account/verification needed, an approval, an announcement that " +
            "affects him. Each action = one short imperative sentence (e.g. 'Reply to Dr. Lee about your thesis topic').\n\n" +
            "Ignore marketing, newsletters, promotions, social notifications, receipts, and vague mentions. Only include what " +
            "genuinely matters to him personally.\n\n" +
            "Output ONLY valid JSON, no prose, exactly this shape:\n" +
            "{\"deadlines\":[{\"title\":\"...\",\"dueIso\":\"YYYY-MM-DDTHH:MM\",\"kind\":\"Exam\",\"critical\":true,\"confidence\":0.0,\"source\":\"email subject\"}]," +
            "\"actions\":[{\"text\":\"one short thing to do\",\"source\":\"email subject\"}]}\n" +
            "Use [] for an empty list.\n\nEmails:\n" + sb;

        string raw = await _claude.CompleteAsync(prompt, ct);
        string json = ClaudeService.ExtractJson(raw);

        try
        {
            var dto = JsonSerializer.Deserialize<ScanDto>(json, JsonOpts);
            if (dto != null)
            {
                scan.Deadlines = (dto.Deadlines ?? new())
                    .Where(x => !string.IsNullOrWhiteSpace(x.Title) && x.Due != null).ToList();
                scan.Actions = (dto.Actions ?? new())
                    .Where(a => !string.IsNullOrWhiteSpace(a.Text)).ToList();
            }
        }
        catch (JsonException) { }

        return scan;
    }

    private sealed class ScanDto
    {
        public List<ExtractedDeadline>? Deadlines { get; set; }
        public List<EmailAction>? Actions { get; set; }
    }

    private sealed class ChecklistDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<StepDto>? Steps { get; set; }
    }

    private sealed class StepDto
    {
        public string? Text { get; set; }
        public string? Ask { get; set; }
        public string? Where { get; set; }
    }
}
