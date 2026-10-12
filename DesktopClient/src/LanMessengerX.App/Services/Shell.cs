using System.Diagnostics;

namespace LanMessengerX.App.Services;

/// <summary>Opening files/folders and showing desktop notifications in a platform independent way (best effort).</summary>
public static class Shell
{
    public static void Open(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS()) Process.Start("open", new[] { path });
            else Process.Start("xdg-open", new[] { path });
        }
        catch (Exception) { }
    }

    public static void Reveal(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start("explorer.exe", $"/select,\"{path}\"");
            else Open(Path.GetDirectoryName(path) ?? path);
        }
        catch (Exception) { }
    }

    public static string FreeFileName(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (int i = 1; ; i++)
        {
            path = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(path)) return path;
        }
    }

    /// <summary>Desktop notification: notify-send on Linux, a toast through PowerShell on Windows.</summary>
    public static void Notify(string title, string text)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo("notify-send") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                psi.ArgumentList.Add("--app-name=LAN Messenger X");
                psi.ArgumentList.Add("--icon=dialog-information");
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(Truncate(text, 300));
                Process.Start(psi);
            }
            else if (OperatingSystem.IsWindows())
            {
                static string X(string s) => System.Security.SecurityElement.Escape(s) ?? "";
                var xml = $"<toast><visual><binding template='ToastGeneric'><text>{X(title)}</text><text>{X(Truncate(text, 300))}</text></binding></visual></toast>";
                var script =
                    "$ErrorActionPreference='Stop';" +
                    "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] | Out-Null;" +
                    "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime] | Out-Null;" +
                    "$x = New-Object Windows.Data.Xml.Dom.XmlDocument;" +
                    $"$x.LoadXml('{xml.Replace("'", "''")}');" +
                    "$t = [Windows.UI.Notifications.ToastNotification]::new($x);" +
                    "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\WindowsPowerShell\\v1.0\\powershell.exe').Show($t)";
                var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-WindowStyle");
                psi.ArgumentList.Add("Hidden");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(script);
                Process.Start(psi);
            }
        }
        catch (Exception) { }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
