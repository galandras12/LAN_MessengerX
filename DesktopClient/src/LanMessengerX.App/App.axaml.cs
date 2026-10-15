using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.App.Views;
using LanMessengerX.Core;

namespace LanMessengerX.App;

public sealed class App : Application
{
    public AppState State { get; private set; } = null!;
    private MainWindow? _main;
    private bool _quitting;
    private TrayIcon? _tray;
    private DateTime _lastTrayClick = DateTime.MinValue;

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
            ApplyTray();
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
            _main.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty && _main.WindowState == WindowState.Minimized && State.Settings.MinimizeToTray && TrayAvailable)
                    _main.Hide();
            };
            bool background = (desktop.Args ?? Array.Empty<string>()).Contains("--background");
            if ((settings.StartMinimized || background) && TrayAvailable) _main.Hide();
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

    /// <summary>(Re)creates the tray icon and its menu in the current language; removes it if the setting says so.</summary>
    public void ApplyTray()
    {
        if (State == null) return;
        try
        {
            if (!State.Settings.TrayIcon)
            {
                TrayIcon.SetIcons(this, new TrayIcons());
                _tray = null;
                TrayAvailable = false;
                return;
            }
            var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://LanMessengerX/Assets/app-256.png")));
            var menu = new NativeMenu();
            NativeMenuItem Item(string text, Action act)
            {
                var i = new NativeMenuItem(text);
                i.Click += (_, _) => act();
                return i;
            }
            menu.Add(Item(Loc.F("&Show {0}", "LAN Messenger X").Replace("&", ""), ShowMain));
            var statusMenu = new NativeMenu();
            foreach (var code in Proto.StatusCodes)
            {
                var c = code;
                var it = new NativeMenuItem(StatusText.Label(c)) { ToggleType = NativeMenuItemToggleType.Radio, IsChecked = State.Status == c };
                it.Click += (_, _) => State.SetStatus(c);
                statusMenu.Add(it);
            }
            menu.Add(new NativeMenuItem(Loc.T("Change Status")) { Menu = statusMenu });
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(Item(Loc.T("History"), () => { ShowMain(); _main?.OpenHistory(); }));
            menu.Add(Item(Loc.T("File Transfers"), () => { ShowMain(); _main?.OpenTransfers(); }));
            menu.Add(Item(Loc.T("Preferences"), () => { ShowMain(); _main?.OpenSettings(); }));
            menu.Add(Item(Loc.T("About"), () => { ShowMain(); _main?.OpenAbout(); }));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(Item(Loc.T("Exit"), Quit));
            var tray = new TrayIcon { Icon = icon, ToolTipText = "LAN Messenger X", Menu = menu };
            tray.Clicked += (_, _) =>
            {
                if (State.Settings.SingleClickTray || (DateTime.Now - _lastTrayClick).TotalMilliseconds < 600) ShowMain();
                _lastTrayClick = DateTime.Now;
            };
            TrayIcon.SetIcons(this, new TrayIcons { tray });
            _tray = tray;
            State.UnreadChanged -= UpdateTrayTip;
            State.UnreadChanged += UpdateTrayTip;
            TrayAvailable = true;
        }
        catch (Exception e)
        {
            FileLog.Write("tray: " + e.Message);
            TrayAvailable = false;
        }
    }

    private void UpdateTrayTip()
    {
        if (_tray != null) _tray.ToolTipText = State.TotalUnread > 0 ? Loc.F("LAN Messenger X - {0} unread", State.TotalUnread) : "LAN Messenger X";
    }
}
