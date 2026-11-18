// RigBuddy.exe --uninstall: what "Apps > Rig Buddy Studio > Uninstall" runs. Removes the firewall rules, the shortcuts, the
// start-with-Windows entry, the "Apps" entry and the install folder (settings, logs and map data are inside it: they go
// only when the user says so). A program cannot delete its own exe, so the last part is a short detached cmd.

using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

static class Uninstaller
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RigBuddyStudio";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "Rig Buddy Studio";

    public static int Run(string[] args)
    {
        string exe = Environment.ProcessPath!;
        string dir = Path.GetDirectoryName(exe)!;
        bool silent = args.Contains("--silent");

        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            // again, as administrator (firewall rules and the "Apps" entry need it)
            try
            {
                Process.Start(new ProcessStartInfo(exe, string.Join(' ', args.Select(a => $"\"{a}\""))) { UseShellExecute = true, Verb = "runas" });
                return 0;
            }
            catch { return 1; }
        }

        bool deleteData = true;
        if (!silent)
        {
            var ask = MessageBox.Show(
                $"{L.T("un.ask", dir)}\n\n{L.T("un.data", Path.Combine(dir, "data"))}",
                AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (ask == DialogResult.Cancel) return 0;
            deleteData = ask == DialogResult.Yes;
        }

        // quit the running copy
        ControlPort.Send("quit");
        for (int i = 0; i < 60 && Mutex.TryOpenExisting(@"Local\RigBuddy.Host", out var m) && Close(m); i++) Thread.Sleep(250);

        foreach (string rule in new[] { "Rig Buddy (Node.js)", "Rig Buddy (discovery)", "Rig Buddy (Studio)" })
            Netsh($"advfirewall firewall delete rule name=\"{rule}\"");
        Netsh($"advfirewall firewall delete rule name=all program=\"{Path.Combine(dir, @"node\node.exe")}\"");
        Netsh($"advfirewall firewall delete rule name=all program=\"{exe}\"");

        foreach (var f in new[] { Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonPrograms })
            try { File.Delete(Path.Combine(Environment.GetFolderPath(f), AppName + ".lnk")); } catch { }
        try { using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true); run?.DeleteValue("Rig Buddy", throwOnMissingValue: false); } catch { }
        Autostart.Set(false);
        try { Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false); } catch { }

        // the rest after this process has ended (it is running from the folder)
        string d = dir.TrimEnd('\\');
        string cmd = deleteData
            ? $"/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"{d}\""
            : $"/c ping -n 3 127.0.0.1 >nul & del /q \"{d}\\*.exe\" \"{d}\\*.dll\" \"{d}\\*.json\" \"{d}\\*.pdb\" & rmdir /s /q \"{d}\\dist\" \"{d}\\node\" \"{d}\\plugin\" \"{d}\\licenses\"";
        Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
        if (!silent) MessageBox.Show(L.T("un.done"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    static bool Close(Mutex m) { m.Dispose(); return true; }

    static void Netsh(string args)
    {
        try { using var p = Process.Start(new ProcessStartInfo("netsh.exe", args) { CreateNoWindow = true, UseShellExecute = false }); p?.WaitForExit(30_000); }
        catch { }
    }
}
