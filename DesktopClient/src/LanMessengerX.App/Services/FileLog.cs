using System.Text;

namespace LanMessengerX.App.Services;

/// <summary>Plain text diagnostics log next to the settings (kept small: truncated at 512 KiB on start).</summary>
public static class FileLog
{
    private static readonly object Gate = new();
    private static readonly Queue<string> Recent = new();

    public static string Path_ => Path.Combine(AppSettings.DataDir, "lanmessengerx.log");

    public static void Reset()
    {
        try
        {
            var f = new FileInfo(Path_);
            if (f.Exists && f.Length > 512 * 1024) f.Delete();
        }
        catch (Exception) { }
        Write($"==== LAN Messenger X {Core.Proto.Version} started, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, culture {System.Globalization.CultureInfo.CurrentCulture.Name}");
    }

    public static void Write(string line)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}";
        lock (Gate)
        {
            Recent.Enqueue(text);
            while (Recent.Count > 200) Recent.Dequeue();
            try { File.AppendAllText(Path_, text + Environment.NewLine, Encoding.UTF8); }
            catch (Exception) { }
        }
    }

    public static string Tail(int lines = 60)
    {
        lock (Gate) return string.Join(Environment.NewLine, Recent.TakeLast(lines));
    }
}
