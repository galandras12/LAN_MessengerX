using System.Diagnostics;

namespace LanMessengerX.App.Services;

/// <summary>
/// Windows Defender Firewall helper. The first time an unsigned program listens on the network Windows asks
/// whether to allow it; closing that dialog (or allowing "Public" only) leaves a block rule behind and nobody can
/// reach the program any more. This removes such rules and adds an allow rule for all profiles (needs admin once).
/// </summary>
public static class Firewall
{
    public static bool Supported => OperatingSystem.IsWindows() && Environment.ProcessPath != null;

    public static bool Allow(out string message)
    {
        message = "";
        var exe = Environment.ProcessPath;
        if (!OperatingSystem.IsWindows() || exe == null) { message = "Only needed on Windows."; return false; }
        var script = Path.Combine(Path.GetTempPath(), "lmx-firewall.cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            $"netsh advfirewall firewall delete rule name=all program=\"{exe}\" >nul\r\n" +
            $"netsh advfirewall firewall add rule name=\"LAN Messenger X\" dir=in action=allow program=\"{exe}\" enable=yes profile=any >nul\r\n" +
            "exit /b %errorlevel%\r\n");
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            using var p = Process.Start(psi);
            p?.WaitForExit(20000);
            var ok = p is { HasExited: true, ExitCode: 0 };
            message = ok ? "Windows Firewall now allows LAN Messenger X on every network profile." : "The firewall rule could not be added.";
            FileLog.Write("firewall: " + message);
            return ok;
        }
        catch (Exception e)
        {
            message = "Cancelled or not permitted: " + e.Message;
            FileLog.Write("firewall: " + message);
            return false;
        }
        finally
        {
            try { File.Delete(script); } catch (Exception) { }
        }
    }
}
