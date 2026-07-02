using System.IO;
using System.Text.Json;
using Compass.Models;

namespace Compass.Services;

/// <summary>Single source of truth. Persists everything to a local JSON file in %APPDATA%\Compass.</summary>
public sealed class DataStore
{
    public static DataStore Instance { get; } = new();

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Compass");
    private static readonly string FilePath = Path.Combine(Dir, "data.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public AppData Data { get; private set; } = new();

    /// <summary>
    /// Raised after a real change is saved (touch:true). App subscribes to kick off an
    /// immediate (debounced) cloud sync, so edits reach the phone without waiting for the timer.
    /// </summary>
    public event Action? Changed;

    private DataStore() { }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                Data = JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
            }
        }
        catch
        {
            Data = new AppData();
        }

        if (!Data.Seeded)
        {
            Seed();
            Data.Seeded = true;
            Save();
        }

        EnsureAccounts();
    }

    // Pre-fill Justin's two email accounts once, so setup is just "add the password / sign in".
    private void EnsureAccounts()
    {
        if (Data.Settings.AccountsSeeded) return;

        if (!Data.Settings.Accounts.Any(a => a.Provider == "gmail"))
            Data.Settings.Accounts.Add(new EmailAccount
            {
                Label = "Gmail",
                Email = "justintirta@gmail.com",
                Provider = "gmail",
                ImapHost = "imap.gmail.com",
                ImapPort = 993,
                AuthType = "password",
            });

        if (!Data.Settings.Accounts.Any(a => a.Provider == "microsoft"))
            Data.Settings.Accounts.Add(new EmailAccount
            {
                Label = "BINUS",
                Email = "justin.tirtawijaya@binus.ac.id",
                Provider = "microsoft",
                ImapHost = "outlook.office365.com",
                ImapPort = 993,
                AuthType = "oauth",
            });

        Data.Settings.AccountsSeeded = true;
        Save();
    }

    public void Save(bool touch = true)
    {
        try
        {
            if (touch) Data.DataUpdatedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, Opts));
        }
        catch
        {
            // Never let a save failure crash the always-on app.
        }

        // A user-driven change (touch) should push to the cloud right away.
        // Sync's own write-backs use touch:false, so this never loops.
        if (touch)
        {
            try { Changed?.Invoke(); } catch { }
        }
    }

    public static string DataFolder => Dir;

    // ---- Seed content: pre-written playbooks so the app is useful on day one ----
    private void Seed()
    {
        Data.Playbooks.Add(new Playbook
        {
            Title = "Apply to a university",
            Description = "The steps most applications share. Do them in order; don't wait until you 'feel ready'.",
            Steps =
            {
                Step("Make a list of every programme you might apply to, with its application open + close dates.",
                     "For each one: when does the application open and close? Is there an early round?",
                     "Each university's official admissions page (not a forum)."),
                Step("Write down the exact requirements for each programme.",
                     "What documents, test scores and minimum grades does this programme require?",
                     "The programme's 'admission requirements' page."),
                Step("Collect your documents early (transcripts, ID, photo, certificates).",
                     "Which documents need to be official/stamped, and how long do they take to get?",
                     "Your school's admin office / the issuing authority."),
                Step("Draft your personal essay / motivation letter, then get one person to read it.",
                     "What is the prompt and word limit? Who can review my draft?",
                     "The application portal + a teacher or mentor."),
                Step("Register for any required entrance test and note the test date as a deadline.",
                     "Is a test required? When is it, and what's the registration deadline?",
                     "The test provider's official site."),
                Step("Submit the application before the deadline — aim for 3 days early.",
                     "Did I get a confirmation email / reference number after submitting?",
                     "The application portal confirmation screen."),
                Step("Track the result date and what to do if accepted or waitlisted.",
                     "When are results announced, and what's the next step if I get in?",
                     "The admissions office."),
            }
        });

        Data.Playbooks.Add(new Playbook
        {
            Title = "Course registration (each term)",
            Description = "So you never miss the registration window or pick the wrong classes.",
            Steps =
            {
                Step("Find the exact registration open and close date/time for this term.",
                     "When does registration open and close (date AND time)?",
                     "Academic calendar / registrar."),
                Step("Check which courses you must take and their prerequisites.",
                     "What am I required to take this term, and have I met the prerequisites?",
                     "Your study plan / academic advisor."),
                Step("Build your timetable and check for clashes before registration opens.",
                     "Do any of my chosen classes overlap in time?",
                     "The course catalogue / timetable tool."),
                Step("Register the moment it opens (popular classes fill fast).",
                     "Is there a backup class if my first choice is full?",
                     "Registration portal."),
                Step("Confirm your registered courses and keep the confirmation.",
                     "Does my confirmed schedule match what I intended?",
                     "Portal confirmation page."),
                Step("Note the add/drop deadline in case you need to change.",
                     "Until when can I add or drop a course without penalty?",
                     "Academic calendar."),
            }
        });

        Data.Playbooks.Add(new Playbook
        {
            Title = "After you're accepted (enrolment)",
            Description = "The stuff nobody tells you to do after the 'congratulations' email.",
            Steps =
            {
                Step("Read the acceptance letter fully and list every deadline in it.",
                     "What must I do to accept my place, and by when?",
                     "The acceptance letter / offer portal."),
                Step("Pay any enrolment deposit / tuition by its deadline.",
                     "How much is due, how do I pay, and what's the deadline?",
                     "Finance / bursar's office."),
                Step("Submit final documents they asked for.",
                     "Which final documents do they still need from me?",
                     "The admissions checklist."),
                Step("Sort out housing / accommodation if needed.",
                     "What are my housing options and the application deadline?",
                     "Housing office."),
                Step("Handle scholarships, loans or financial aid paperwork.",
                     "What financial aid am I eligible for and what's the deadline?",
                     "Financial aid office."),
                Step("Note orientation / first-day dates.",
                     "When is orientation and is it mandatory?",
                     "The welcome / orientation email."),
            }
        });

        // A friendly starter item so the app isn't empty.
        Data.Inbox.Add(new CaptureItem
        {
            Text = "Tip: whenever anyone mentions a date, dump it here immediately — sort it out later.",
        });
    }

    private static PlaybookStep Step(string text, string ask, string where) =>
        new() { Text = text, Ask = ask, WhereToFind = where };
}
