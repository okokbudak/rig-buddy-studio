// "Start with Windows". Rig Buddy runs as administrator (app.manifest), and Windows does not start such a program from the
// Run key at logon (UAC holds it back without asking), so the entry is a scheduled task: at this user's logon, with the
// highest rights, no prompt. An older install's Run key entry is taken over.

using System.Diagnostics;
using Microsoft.Win32;

static class Autostart
{
    const string Task = "Rig Buddy";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Rig Buddy";

    static (int Code, string Output) Schtasks(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15_000);
            return (p.ExitCode, output);
        }
        catch (Exception e)
        {
            return (-1, e.Message);
        }
    }

    static string User => $"{Environment.UserDomainName}\\{Environment.UserName}";

    static bool? _enabled;
    static DateTime _checkedAt;

    /** Asked twice a second by the window: schtasks is run at most every 30 seconds (and after a change). */
    public static bool Enabled
    {
        get
        {
            if (_enabled == null || DateTime.UtcNow - _checkedAt > TimeSpan.FromSeconds(30))
            {
                _enabled = Schtasks($"/Query /TN \"{Task}\"").Code == 0;
                _checkedAt = DateTime.UtcNow;
            }
            return _enabled.Value;
        }
    }

    /** Points an existing entry (a Run key value of an older install, a task of another copy) at this exe. */
    public static void Migrate()
    {
        bool fromRunKey = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(RunValue) is string)
            {
                key.DeleteValue(RunValue, throwOnMissingValue: false);
                fromRunKey = true;
            }
        }
        catch (Exception) { }
        if (fromRunKey) { Set(true); return; }
        var (code, xml) = Schtasks($"/Query /TN \"{Task}\" /XML");
        if (code == 0 && !xml.Contains(Environment.ProcessPath!, StringComparison.OrdinalIgnoreCase)) Set(true);
    }

    public static void Set(bool on)
    {
        _enabled = null;
        if (!on)
        {
            Schtasks($"/Delete /TN \"{Task}\" /F");
            return;
        }
        string exe = Environment.ProcessPath!;
        var (code, output) = Schtasks($"/Create /F /TN \"{Task}\" /SC ONLOGON /RU \"{User}\" /IT /RL HIGHEST /TR \"\\\"{exe}\\\" --autostart\"");
        if (code != 0) Console.WriteLine($"autostart: {output.Trim()}");
    }
}
