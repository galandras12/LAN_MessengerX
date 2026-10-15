namespace LanMessengerX.App.Services;

/// <summary>"Start with Windows": a value under HKCU\...\Run (Windows), an autostart .desktop file (Linux).</summary>
public static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LAN Messenger X";

    public static void Set(bool on)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe == null) return;
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
                if (key == null) return;
                if (on) key.SetValue(ValueName, "\"" + exe + "\" --background");
                else key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else if (OperatingSystem.IsLinux())
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart");
                var file = Path.Combine(dir, "lanmessengerx.desktop");
                if (on)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(file, $"[Desktop Entry]\nType=Application\nName=LAN Messenger X\nExec=\"{exe}\" --background\nX-GNOME-Autostart-enabled=true\n");
                }
                else if (File.Exists(file)) File.Delete(file);
            }
        }
        catch (Exception e) { FileLog.Write("autostart: " + e.Message); }
    }
}
