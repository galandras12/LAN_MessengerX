using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using LanMessengerX.App.Services;
using LanMessengerX.App.Views;

namespace LanMessengerX.App;

public sealed class App : Application
{
    public AppState State { get; private set; } = null!;
    private MainWindow? _main;
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettings.Load();
            ApplyTheme(settings.Theme);
            State = new AppState(settings);
            _main = new MainWindow { DataContext = State };
            desktop.MainWindow = _main;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => State.Shutdown();
            State.Start();
            SetupTray();
            _main.Width = settings.WindowWidth;
            _main.Height = settings.WindowHeight;
            _main.Closing += (_, e) =>
            {
                settings.WindowWidth = _main.Width;
                settings.WindowHeight = _main.Height;
                if (!_quitting && State.Settings.CloseToTray && TrayAvailable)
                {
                    e.Cancel = true;
                    _main.Hide();
                }
            };
            if (settings.StartMinimized && TrayAvailable) _main.Hide();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public bool TrayAvailable { get; private set; }

    public void ApplyTheme(string theme) =>
        RequestedThemeVariant = theme switch { "light" => ThemeVariant.Light, "dark" => ThemeVariant.Dark, _ => ThemeVariant.Default };

    public void ShowMain()
    {
        if (_main == null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void Quit()
    {
        _quitting = true;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown();
    }

    private void SetupTray()
    {
        try
        {
            var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://LanMessengerX/Assets/app-256.png")));
            var menu = new NativeMenu();
            var open = new NativeMenuItem("Open LAN Messenger X");
            open.Click += (_, _) => ShowMain();
            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) => Quit();
            menu.Add(open);
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(quit);
            var tray = new TrayIcon { Icon = icon, ToolTipText = "LAN Messenger X", Menu = menu };
            tray.Clicked += (_, _) => ShowMain();
            TrayIcon.SetIcons(this, new TrayIcons { tray });
            State.UnreadChanged += () => tray.ToolTipText = State.TotalUnread > 0 ? $"LAN Messenger X - {State.TotalUnread} unread" : "LAN Messenger X";
            TrayAvailable = true;
        }
        catch (Exception)
        {
            TrayAvailable = false;
        }
    }
}
