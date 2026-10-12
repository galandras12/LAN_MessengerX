using Avalonia;
using LanMessengerX.App.Services;

namespace LanMessengerX.App;

internal static class Program
{
    private static FileStream? _instanceLock;

    [STAThread]
    public static int Main(string[] args)
    {
        // One instance per user: the lock file stays open (and locked) for the lifetime of the process.
        try
        {
            var path = Path.Combine(AppSettings.DataDir, "instance.lock");
            _instanceLock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            Console.Error.WriteLine("LAN Messenger X is already running.");
            return 1;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
