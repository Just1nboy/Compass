using System.Text.Json;
using Compass.Models;
using Microsoft.Maui.Storage;

namespace Compass.Mobile.Services;

/// <summary>Local persistence for the phone. Cloud sync layers on top of this later.</summary>
public sealed class MobileStore
{
    public static MobileStore Instance { get; } = new();

    private readonly string _file;
    public AppData Data { get; private set; } = new();

    /// <summary>
    /// Raised after a real change is saved (touch:true). App subscribes to kick off an
    /// immediate (debounced) cloud sync, so edits reach the PC without waiting for the timer.
    /// </summary>
    public event Action? Changed;

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    private MobileStore()
    {
        _file = Path.Combine(FileSystem.AppDataDirectory, "data.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_file))
                Data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(_file)) ?? new AppData();
        }
        catch { Data = new AppData(); }

        if (!Data.Seeded)
        {
            Seed();
            Data.Seeded = true;
            Save();
        }
    }

    public void Save(bool touch = true)
    {
        try
        {
            if (touch) Data.DataUpdatedUtc = DateTime.UtcNow;
            File.WriteAllText(_file, JsonSerializer.Serialize(Data, Opts));
        }
        catch { }

        // A user-driven change (touch) should push to the cloud right away.
        // Sync's own write-backs use touch:false, so this never loops.
        if (touch)
        {
            try { Changed?.Invoke(); } catch { }
        }
    }

    private void Seed()
    {
        Data.Playbooks.Add(new Playbook
        {
            Title = "Apply to a university",
            Description = "The core steps most applications share.",
            Steps =
            {
                new PlaybookStep { Text = "List every programme + its open/close dates.", Ask = "When does it open and close?", WhereToFind = "The official admissions page." },
                new PlaybookStep { Text = "Write down each programme's exact requirements.", Ask = "What documents and grades are required?", WhereToFind = "The 'admission requirements' page." },
                new PlaybookStep { Text = "Collect documents early (transcripts, ID, photo).", Ask = "Which need to be official/stamped?", WhereToFind = "Your school office." },
            }
        });
        Data.Inbox.Add(new CaptureItem
        {
            Text = "Welcome! Tap + to add a deadline. Once sync is set up, these appear on your PC too."
        });
    }
}
