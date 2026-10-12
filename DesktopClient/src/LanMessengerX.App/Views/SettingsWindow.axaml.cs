using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Views;

public partial class SettingsWindow : Window
{
    private AppState State => (AppState)DataContext!;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not AppState s) return;
            NameBox.Text = s.Name;
            NoteBox.Text = s.Note;
            StatusBox.ItemsSource = Proto.StatusCodes.Select(c => new StatusChoice(c)).ToList();
            StatusBox.SelectedIndex = Math.Max(0, Array.IndexOf(Proto.StatusCodes, s.Status));
            HistoryBox.IsChecked = s.Settings.SaveHistory;
            NotifyBox.IsChecked = s.Settings.Notifications;
            TrayBox.IsChecked = s.Settings.CloseToTray;
            MinimizedBox.IsChecked = s.Settings.StartMinimized;
            ThemeBox.SelectedIndex = s.Settings.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
            DownloadBox.Text = s.Settings.EffectiveDownloadDir;
            NetText.Text = s.Connected
                ? $"Connected as {s.Ip}\nUDP and TCP port {s.Settings.Port}, multicast group {Proto.DefaultMulticast}\nYour id on the network: {s.LocalId}"
                : "Not connected. Join a network to find other people.";
            AboutTitle.Text = $"LAN Messenger X {Proto.Version}";
        };
    }

    private async void OnChoosePicture(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a profile picture",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        try
        {
            using var src = new Bitmap(path);
            var target = new RenderTargetBitmap(new Avalonia.PixelSize(128, 128), new Avalonia.Vector(96, 96));
            using (var ctx = target.CreateDrawingContext())
            {
                double side = Math.Min(src.Size.Width, src.Size.Height);
                var from = new Avalonia.Rect((src.Size.Width - side) / 2, (src.Size.Height - side) / 2, side, side);
                ctx.DrawImage(src, from, new Avalonia.Rect(0, 0, 128, 128));
            }
            target.Save(State.AvatarFile);
            State.SetAvatar(State.AvatarFile);
        }
        catch (Exception) { }
    }

    private void OnRemovePicture(object? sender, RoutedEventArgs e) => State.SetAvatar(null);

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var dirs = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Save received files to", AllowMultiple = false });
        if (dirs.Count > 0 && dirs[0].TryGetLocalPath() is { } p) DownloadBox.Text = p;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var s = State;
        s.SetProfile(NameBox.Text ?? "", NoteBox.Text ?? "");
        if (StatusBox.SelectedItem is StatusChoice sc && sc.Code != s.Status) s.SetStatus(sc.Code);
        s.Settings.SaveHistory = HistoryBox.IsChecked == true;
        s.Settings.Notifications = NotifyBox.IsChecked == true;
        s.Settings.CloseToTray = TrayBox.IsChecked == true;
        s.Settings.StartMinimized = MinimizedBox.IsChecked == true;
        var theme = ThemeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        if (theme != s.Settings.Theme)
        {
            s.Settings.Theme = theme;
            (Avalonia.Application.Current as App)?.ApplyTheme(theme);
        }
        var dl = DownloadBox.Text?.Trim();
        s.Settings.DownloadDir = string.IsNullOrEmpty(dl) || dl == new AppSettings().EffectiveDownloadDir ? null : dl;
        s.Settings.Save();
        Close();
    }
}
