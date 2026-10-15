using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace LanMessengerX.App.Services;

public enum SoundEvent { NewMessage, UserOnline, UserOffline, NewFile, FileDone }

/// <summary>The five sounds of the original LAN Messenger. Windows: winmm PlaySound; elsewhere paplay/aplay if installed.</summary>
public static class Sounds
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? name, IntPtr module, uint flags);

    private static readonly Dictionary<SoundEvent, string> Files = new();

    private static string File_(SoundEvent e) => e switch
    {
        SoundEvent.NewMessage => "newmessage", SoundEvent.UserOnline => "useronline", SoundEvent.UserOffline => "useroffline",
        SoundEvent.NewFile => "newfile", _ => "filedone",
    };

    private static string Extract(SoundEvent e)
    {
        lock (Files)
        {
            if (Files.TryGetValue(e, out var p) && File.Exists(p)) return p;
            var dir = Path.Combine(Path.GetTempPath(), "LanMessengerX-sounds");
            Directory.CreateDirectory(dir);
            p = Path.Combine(dir, File_(e) + ".wav");
            using (var src = AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/sounds/{File_(e)}.wav")))
            using (var dst = File.Create(p)) src.CopyTo(dst);
            Files[e] = p;
            return p;
        }
    }

    public static void Play(AppSettings s, string myStatus, SoundEvent e)
    {
        if (!s.Sounds) return;
        if (s.NoBusySound && myStatus == "busy") return;
        if (s.NoDndSound && myStatus == "dnd") return;
        bool on = e switch
        {
            SoundEvent.NewMessage => s.SoundNewMessage, SoundEvent.UserOnline => s.SoundUserOnline, SoundEvent.UserOffline => s.SoundUserOffline,
            SoundEvent.NewFile => s.SoundNewFile, _ => s.SoundFileDone,
        };
        if (!on) return;
        Task.Run(() =>
        {
            try
            {
                var file = Extract(e);
                if (OperatingSystem.IsWindows()) PlaySound(file, IntPtr.Zero, 0x20001 /* SND_FILENAME | SND_ASYNC */);
                else
                {
                    foreach (var player in new[] { "paplay", "aplay" })
                    {
                        try { Process.Start(new ProcessStartInfo(player, $"\"{file}\"") { UseShellExecute = false, RedirectStandardError = true })?.Dispose(); return; }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception ex) { FileLog.Write("sound: " + ex.Message); }
        });
    }
}
