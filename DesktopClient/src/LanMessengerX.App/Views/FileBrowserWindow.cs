using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanMessengerX.App.Localization;

namespace LanMessengerX.App.Views;

/// <summary>A file / folder chooser drawn by the app, for systems where the platform dialog is not available.</summary>
public sealed class FileBrowserWindow : Window
{
    public enum Mode { File, Files, Folder }

    private sealed record Entry(string Path, string Label, bool IsDir)
    {
        public override string ToString() => Label;
    }

    private readonly Mode _mode;
    private readonly bool _images;
    private readonly TextBox _path = new();
    private readonly ListBox _list = new();
    private readonly Button _ok;
    private string _dir = "";
    private IReadOnlyList<string> _result = Array.Empty<string>();

    public static async Task<IReadOnlyList<string>> Show(Window owner, string title, Mode mode, bool imagesOnly)
    {
        var w = new FileBrowserWindow(title, mode, imagesOnly);
        await w.ShowDialog(owner);
        return w._result;
    }

    private FileBrowserWindow(string title, Mode mode, bool images)
    {
        _mode = mode;
        _images = images;
        Title = title;
        Width = 680; Height = 520; MinWidth = 440; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _list.SelectionMode = mode == Mode.Files ? SelectionMode.Multiple : SelectionMode.Single;

        var up = new Button { Content = "↑", Width = 40, Margin = new Thickness(0, 0, 6, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
        up.Click += (_, _) => Up();
        var go = new Button { Content = "→", Width = 40, Margin = new Thickness(6, 0, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
        go.Click += (_, _) => Navigate(_path.Text ?? "");
        _path.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Navigate(_path.Text ?? ""); };
        var bar = new DockPanel { Margin = new Thickness(12, 12, 12, 8) };
        DockPanel.SetDock(up, Dock.Left);
        DockPanel.SetDock(go, Dock.Right);
        bar.Children.Add(up);
        bar.Children.Add(go);
        bar.Children.Add(_path);

        var places = new StackPanel { Spacing = 2, Margin = new Thickness(12, 0, 8, 12), Width = 150 };
        void Place(string label, string? dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent };
            b.Click += (_, _) => Navigate(dir);
            places.Children.Add(b);
        }
        Place(Loc.T("Desktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Place(Loc.T("Documents"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Place(Loc.T("Pictures"), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Place(Loc.T("Downloads"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        Place(Loc.T("Home"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try { if (d.IsReady) Place(d.Name, d.RootDirectory.FullName); } catch (Exception) { }
            }
        }
        catch (Exception) { }

        _list.DoubleTapped += (_, _) => ActivateEntry(_list.SelectedItem as Entry);
        _list.SelectionChanged += (_, _) => UpdateOk();
        _ok = new Button { Content = Loc.T(mode == Mode.Folder ? "Select folder" : "Open"), MinWidth = 88, Padding = new Thickness(16, 7), HorizontalContentAlignment = HorizontalAlignment.Center };
        _ok.Classes.Add("accent");
        _ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = Loc.T("Cancel"), MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(12) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(_ok);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(places, Dock.Left);
        root.Children.Add(bar);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = places, [DockPanel.DockProperty] = Dock.Left });
        root.Children.Add(_list);
        Content = root;

        var start = Environment.GetFolderPath(images ? Environment.SpecialFolder.MyPictures : Environment.SpecialFolder.UserProfile);
        Navigate(Directory.Exists(start) ? start : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private void Navigate(string dir)
    {
        try
        {
            dir = dir.Trim().Trim('"');
            if (!Directory.Exists(dir)) return;
            var items = new List<Entry>();
            foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
            {
                var name = Path.GetFileName(d);
                try { if ((new DirectoryInfo(d).Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; } catch (Exception) { }
                items.Add(new Entry(d, "📁  " + name, true));
            }
            if (_mode != Mode.Folder)
            {
                foreach (var f in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                {
                    if (_images && !Services.Pickers.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                    try { if ((File.GetAttributes(f) & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; } catch (Exception) { }
                    items.Add(new Entry(f, "      " + Path.GetFileName(f), false));
                }
            }
            _dir = dir;
            _path.Text = dir;
            _list.ItemsSource = items;
            UpdateOk();
        }
        catch (Exception) { /* no access: stay where we are */ }
    }

    private void Up()
    {
        var parent = Directory.GetParent(_dir);
        if (parent != null) Navigate(parent.FullName);
    }

    private void UpdateOk()
    {
        if (_mode == Mode.Folder) { _ok.IsEnabled = _dir.Length > 0; return; }
        _ok.IsEnabled = _list.SelectedItems?.OfType<Entry>().Any(e => !e.IsDir) == true;
    }

    private void ActivateEntry(Entry? e)
    {
        if (e == null) return;
        if (e.IsDir) Navigate(e.Path);
        else Accept();
    }

    private void Accept()
    {
        if (_mode == Mode.Folder)
        {
            var sel = _list.SelectedItem as Entry;
            _result = new[] { sel is { IsDir: true } ? sel.Path : _dir };
        }
        else
        {
            _result = _list.SelectedItems?.OfType<Entry>().Where(e => !e.IsDir).Select(e => e.Path).ToList() ?? new List<string>();
            if (_result.Count == 0) return;
        }
        Close();
    }
}
