using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Compass.Services;

/// <summary>Registers the app to launch automatically at Windows sign-in.
///
/// The launch mechanisms themselves (Run key, Startup-folder .vbs) are only half the
/// story on Windows 11: Explorer consults <c>Explorer\StartupApproved</c> (the list that
/// Task Manager's "Startup apps" page edits) and only launches items that carry an
/// explicit "enabled" record there. An item with NO record is silently skipped at logon.
/// Verified on Justin's machine 2026-07-03 via the Shell-Core operational log: every app
/// that launched at boot had a 02 record, every 03 record was skipped, and Compass — the
/// only startup item with no record at all — was never launched (exe never read, no crash).
/// That missing approval record, plus the separate WPF font-cache crash fixed earlier in
/// App.xaml.cs, is why every mechanism we tried appeared to "silently do nothing".
///
/// The layers are: a DELAYED scheduled task (primary — see
/// <see cref="EnsureScheduledTask"/>), plus the Startup-folder .vbs and Run key with
/// StartupApproved records (backups). The single-instance mutex in App.OnStartup makes
/// it safe when more than one path fires.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    private const string ValueName = "Compass";
    private const string TaskName = "Compass Autostart";
    private const string VbsName = "Compass.vbs";
    private const string OldLnkName = "Compass.lnk";

    // 12-byte StartupApproved blob: first byte 02 = enabled, rest zero.
    // (Disabled is 03 followed by a FILETIME of when it was switched off.)
    private static readonly byte[] ApprovedEnabled = new byte[12] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public static void EnsureAutoStart()
    {
        EnsureScheduledTask();
        EnsureStartupVbs();
        EnsureRunKey();
        ApproveStartup();
        RemoveLegacyMechanisms();
    }

    /// <summary>PRIMARY mechanism: a logon Scheduled Task that starts Compass 2 minutes
    /// AFTER sign-in, retrying up to 5 times a minute apart if the launch fails.
    ///
    /// (Historical note: the long "won't start at boot" saga turned out to be a deploy
    /// problem, not a launch problem — the dev tooling's sandbox had been writing the exe
    /// and data into an overlay only its own processes could see, so at logon the REAL
    /// disk had no exe and every mechanism honestly reported file-not-found. Deploys must
    /// land on the real filesystem; see the project memory for the staging procedure.)
    ///
    /// The delayed task is still the primary launcher because it starts after logon settles,
    /// restarts itself on failure, and records a Last Result we can read after a bad boot
    /// (267009 / 0x41301 "still running" is the success signature while the app is alive).
    ///
    /// The XML matters: schtasks' simple /SC ONLOGON form can't express the delay, and the
    /// defaults would sabotage a tray app — ExecutionTimeLimit kills the task after 72h
    /// (PT0S = run forever) and DisallowStartIfOnBatteries skips the launch on battery,
    /// which on a laptop means "usually". Element order follows the exported-task schema
    /// order; schtasks validates it strictly.</summary>
    private static void EnsureScheduledTask()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            string xml = $"""
                <?xml version="1.0" encoding="UTF-16"?>
                <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                  <Triggers>
                    <LogonTrigger>
                      <Enabled>true</Enabled>
                      <UserId>{user}</UserId>
                      <Delay>PT2M</Delay>
                    </LogonTrigger>
                  </Triggers>
                  <Principals>
                    <Principal id="Author">
                      <UserId>{user}</UserId>
                      <LogonType>InteractiveToken</LogonType>
                      <RunLevel>LeastPrivilege</RunLevel>
                    </Principal>
                  </Principals>
                  <Settings>
                    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                    <AllowHardTerminate>false</AllowHardTerminate>
                    <StartWhenAvailable>true</StartWhenAvailable>
                    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                    <AllowStartOnDemand>true</AllowStartOnDemand>
                    <Enabled>true</Enabled>
                    <Hidden>false</Hidden>
                    <RunOnlyIfIdle>false</RunOnlyIfIdle>
                    <WakeToRun>false</WakeToRun>
                    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                    <Priority>7</Priority>
                    <RestartOnFailure>
                      <Interval>PT1M</Interval>
                      <Count>5</Count>
                    </RestartOnFailure>
                  </Settings>
                  <Actions Context="Author">
                    <Exec>
                      <Command>{exe}</Command>
                    </Exec>
                  </Actions>
                </Task>
                """;

            string tmp = Path.Combine(Path.GetTempPath(), "compass-task.xml");
            File.WriteAllText(tmp, xml, Encoding.Unicode);
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F");
            try { File.Delete(tmp); } catch { }
        }
        catch { }
    }

    /// <summary>Mark both launch mechanisms as explicitly enabled in StartupApproved.
    /// Overwrites a Task-Manager "disable" too: Compass is a safety net, and the in-app
    /// setting (which calls <see cref="Disable"/>) is the intended way to turn this off.</summary>
    private static void ApproveStartup()
    {
        SetApproved(ApprovedRunKey, ValueName);
        SetApproved(ApprovedFolderKey, VbsName);
    }

    private static void SetApproved(string subKey, string valueName)
    {
        try
        {
            using RegistryKey key =
                Registry.CurrentUser.OpenSubKey(subKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(subKey);

            key.SetValue(valueName, ApprovedEnabled, RegistryValueKind.Binary);
        }
        catch { }
    }

    private static void RemoveApproved(string subKey, string valueName)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch { }
    }

    /// <summary>Write a Startup-folder .vbs that launches the exe via WScript.Shell.Run.
    ///
    /// Defensive shape: rather than one launch attempt, the script retries every 2s for
    /// up to 2 minutes, suppresses WSH's modal error dialog (On Error Resume Next — a
    /// launch failure at boot must never greet the user with an error box), and appends
    /// every failed attempt to startup-log.txt so a bad boot leaves evidence instead of
    /// a mystery. (The 80070002 "file not found" this once threw at logon was real: the
    /// exe had never actually been deployed to the real disk — see EnsureScheduledTask.)
    ///
    /// We deliberately do NOT set WshShell.CurrentDirectory (it threw 80070003 at logon
    /// here, and Compass uses absolute paths everywhere).</summary>
    private static void EnsureStartupVbs()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string vbsPath = Path.Combine(startup, VbsName);
            string logPath = Path.Combine(DataStore.DataFolder, "startup-log.txt");

            // In VBScript a literal double-quote inside a string is written as "".
            // """" is the one-character string ", so """" & exe & """" = "<exe>" (quoted
            // so spaces in the path are safe).
            var sb = new StringBuilder();
            sb.AppendLine("On Error Resume Next");
            sb.AppendLine("Set sh = CreateObject(\"WScript.Shell\")");
            sb.AppendLine("Set fso = CreateObject(\"Scripting.FileSystemObject\")");
            sb.AppendLine("exe = \"" + exe + "\"");
            sb.AppendLine("logFile = \"" + logPath + "\"");
            sb.AppendLine();
            sb.AppendLine("Sub Log(msg)");
            sb.AppendLine("    On Error Resume Next");
            sb.AppendLine("    Dim f");
            sb.AppendLine("    Set f = fso.OpenTextFile(logFile, 8, True)");
            sb.AppendLine("    f.WriteLine Now & \"  [vbs] \" & msg");
            sb.AppendLine("    f.Close");
            sb.AppendLine("End Sub");
            sb.AppendLine();
            sb.AppendLine("For i = 1 To 60");
            sb.AppendLine("    Err.Clear");
            sb.AppendLine("    sh.Run \"\"\"\" & exe & \"\"\"\", 0, False");
            sb.AppendLine("    If Err.Number = 0 Then");
            sb.AppendLine("        If i > 1 Then Log \"launched on attempt \" & i");
            sb.AppendLine("        WScript.Quit 0");
            sb.AppendLine("    End If");
            sb.AppendLine("    Log \"attempt \" & i & \" failed: 0x\" & Hex(Err.Number) & \" \" & Err.Description");
            sb.AppendLine("    WScript.Sleep 2000");
            sb.AppendLine("Next");
            sb.AppendLine("Log \"gave up after 60 attempts\"");

            File.WriteAllText(vbsPath, sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
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

    /// <summary>Remove the old Compass.lnk shortcut (superseded by the .vbs) so it
    /// can't linger and confuse things.</summary>
    private static void RemoveLegacyMechanisms()
    {
        try
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string lnk = Path.Combine(startup, OldLnkName);
            if (File.Exists(lnk)) File.Delete(lnk);
        }
        catch { }

        RemoveApproved(ApprovedFolderKey, OldLnkName);
    }

    private static void RunSchtasks(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch { }
    }

    public static bool IsEnabled()
    {
        try
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (File.Exists(Path.Combine(startup, VbsName))) return true;
        }
        catch { }

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

        try
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            foreach (string name in new[] { VbsName, OldLnkName })
            {
                string p = Path.Combine(startup, name);
                if (File.Exists(p)) File.Delete(p);
            }
        }
        catch { }

        RemoveApproved(ApprovedRunKey, ValueName);
        RemoveApproved(ApprovedFolderKey, VbsName);
        RemoveApproved(ApprovedFolderKey, OldLnkName);
        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }
}
