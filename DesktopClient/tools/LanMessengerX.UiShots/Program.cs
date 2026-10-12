using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using LanMessengerX.App;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.App.Views;

// Renders the real windows with demo data to PNG files (headless Skia), so the UI can be reviewed without a display.
// Usage: dotnet run -- <output-dir>
var outDir = args.Length > 0 ? args[0] : "shots";
Directory.CreateDirectory(outDir);
var tmp = Path.Combine(Path.GetTempPath(), "lmx-shots-" + Environment.ProcessId);
Directory.CreateDirectory(tmp);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmp);
Environment.SetEnvironmentVariable("APPDATA", tmp);
Environment.SetEnvironmentVariable("HOME", tmp);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

void Shot(Window w, string name)
{
    Dispatcher.UIThread.RunJobs();
    var bmp = w.CaptureRenderedFrame();
    bmp?.Save(Path.Combine(outDir, name + ".png"));
    Console.WriteLine("wrote " + name + (bmp == null ? " (no frame!)" : ""));
}

Dispatcher.UIThread.Invoke(() =>
{
    var settings = new AppSettings { Name = "Me", MachineId = "AABBCCDDEEFF" };
    var state = new AppState(settings);
    state.LoadDemo();
    var app = (App)Application.Current!;
    foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
    {
        app.RequestedThemeVariant = variant;
        string v = variant == ThemeVariant.Light ? "light" : "dark";
        var main = new MainWindow { DataContext = state, Width = 1000, Height = 680 };
        main.Show();
        Shot(main, "main-" + v);
        main.Close();
    }
    app.RequestedThemeVariant = ThemeVariant.Light;
    var main2 = new MainWindow { DataContext = state, Width = 1000, Height = 680 };
    main2.Show();
    state.Selected = state.Rooms[0];
    Shot(main2, "room-light");
    state.Selected = null;
    Shot(main2, "empty-light");
    main2.Close();
    var settingsWin = new SettingsWindow { DataContext = state };
    settingsWin.Show();
    Shot(settingsWin, "settings-light");
    settingsWin.Close();
});
Console.WriteLine("done");
