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
    var lang = Environment.GetEnvironmentVariable("LMX_LANG") ?? "en_US";
    var settings = new AppSettings { Name = "Me", MachineId = "AABBCCDDEEFF", Language = lang, StockAvatar = 3 };
    var state = new AppState(settings);
    state.LoadDemo();
    var app = (App)Application.Current!;
    string suffix = lang == "en_US" ? "" : "-" + lang;
    foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
    {
        app.RequestedThemeVariant = variant;
        string v = variant == ThemeVariant.Light ? "light" : "dark";
        var main = new MainWindow { DataContext = state, Width = 1000, Height = 680 };
        main.Show();
        Shot(main, "main-" + v + suffix);
        main.Close();
    }
    app.RequestedThemeVariant = ThemeVariant.Light;
    var main2 = new MainWindow { DataContext = state, Width = 1000, Height = 680 };
    main2.Show();
    state.Selected = state.Rooms[0];
    Shot(main2, "room-light" + suffix);
    var ab = main2.FindControl<Button>("AvatarButton")!;
    ab.Flyout!.ShowAt(ab);
    Dispatcher.UIThread.RunJobs();
    Shot(main2, "profile-flyout" + suffix);
    ab.Flyout!.Hide();
    state.Selected = state.PublicChat;
    Shot(main2, "public-light" + suffix);
    state.Selected = null;
    Shot(main2, "empty-light" + suffix);
    main2.Close();
    var names = new[] { "general", "account", "messages", "history", "alerts", "network", "files", "look", "keys" };
    var settingsWin = new SettingsWindow { DataContext = state };
    settingsWin.Show();
    var nav = settingsWin.FindControl<ListBox>("Nav")!;
    for (int i = 0; i < names.Length; i++)
    {
        nav.SelectedIndex = i;
        Shot(settingsWin, "settings-" + names[i] + suffix);
    }
    settingsWin.Close();
    var tw = new TransfersWindow { DataContext = state };
    tw.Show();
    Shot(tw, "transfers" + suffix);
    tw.Close();
});
Console.WriteLine("done");
