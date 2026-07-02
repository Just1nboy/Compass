using System.IO;
using Microsoft.Win32;

namespace Compass.Services;

/// <summary>Registers the app to launch automatically at Windows sign-in.
/// Uses BOTH the per-user Run key AND a Startup-folder shortcut, because the Run key
/// alone is sometimes not honoured; the shortcut is the reliable belt-and-suspenders.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Compass";

    public static void EnsureAutoStart()
    {
        EnsureRunKey();
        EnsureStartupShortcut();
    }

    private static void EnsureRunKey()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            using RegistryKey key =
                Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey);

            key.SetValue(ValueName, $"\"{exe}\"");
        }
        catch { }
    }

    private static void EnsureStartupShortcut()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string lnk = Path.Combine(startup, "Compass.lnk");

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic sc = shell.CreateShortcut(lnk);
            sc.TargetPath = exe;
            sc.WorkingDirectory = Path.GetDirectoryName(exe);
            sc.Description = "Compass — never miss what matters";
            sc.Save();
        }
        catch { }
    }

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    public static void Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }
}
