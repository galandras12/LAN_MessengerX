using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using LanMessengerX.App.Controls;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Views;

/// <summary>Small modal dialogs, built in code to keep the XAML surface of the app small.</summary>
public static class Dialogs
{
    private static Window Make(string title, double width, double height) => new()
    {
        Title = title,
        Width = width,
        Height = height,
        MinWidth = 320,
        CanResize = true,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ShowInTaskbar = false,
        FlowDirection = Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
    };

    private static Button TextButton(string label, Action click, bool accent = false)
    {
        var b = new Button { Content = label, MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (accent) b.Classes.Add("accent");
        b.Padding = new Thickness(16, 7);
        b.Click += (_, _) => click();
        return b;
    }

    private static StackPanel ButtonRow(params Control[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var b in buttons) row.Children.Add(b);
        return row;
    }

    // ---- simple ones -------------------------------------------------------------------------------

    public static Task Message(Window owner, string title, string text)
    {
        var w = Make(title, 420, 190);
        w.CanResize = false;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(ButtonRow(TextButton(Loc.T("OK"), w.Close, accent: true)));
        w.Content = panel;
        return w.ShowDialog(owner);
    }

    public static async Task<bool> Confirm(Window owner, string title, string text, string yes)
    {
        var w = Make(title, 440, 200);
        w.CanResize = false;
        bool result = false;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(ButtonRow(TextButton(Loc.T("Cancel"), w.Close), TextButton(yes, () => { result = true; w.Close(); }, accent: true)));
        w.Content = panel;
        await w.ShowDialog(owner);
        return result;
    }

    /// <summary>Asks for one line of text; null when cancelled.</summary>
    public static async Task<string?> Prompt(Window owner, string title, string label, string initial)
    {
        var w = Make(title, 420, 190);
        w.CanResize = false;
        string? result = null;
        var box = new TextBox { Text = initial, MaxLength = 60 };
        void Ok() { result = box.Text ?? ""; w.Close(); }
        box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Ok(); };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(ButtonRow(TextButton(Loc.T("Cancel"), w.Close), TextButton(Loc.T("OK"), Ok, accent: true)));
        w.Content = panel;
        w.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        await w.ShowDialog(owner);
        return result;
    }

    // ---- broadcast ---------------------------------------------------------------------------------

    /// <summary>Broadcast message: choose the recipients (everyone online is preselected, or only <paramref name="onlyId"/>).</summary>
    public static Task Broadcast(Window owner, AppState state, string? onlyId)
    {
        var w = Make(Loc.T("Send Broadcast Message"), 460, 560);
        var people = state.Contacts.Where(c => !Proto.IsOffline(c.Status)).ToList();
        var selected = new HashSet<string>(onlyId != null ? new[] { onlyId } : people.Select(p => p.Id));
        var list = new StackPanel { Spacing = 1 };
        var info = new TextBlock { Opacity = 0.7 };
        var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 140, Watermark = Loc.T("Message") };
        var send = TextButton(Loc.T("Send"), () => { }, accent: true);
        void Refresh()
        {
            info.Text = Loc.F("Sent to {0} of {1} people who are online right now.", selected.Count, people.Count);
            send.IsEnabled = selected.Count > 0 && !string.IsNullOrWhiteSpace(box.Text);
        }
        var checks = new List<CheckBox>();
        foreach (var c in people)
        {
            var cb = new CheckBox { IsChecked = selected.Contains(c.Id), VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
            cb.IsCheckedChanged += (_, _) => { if (cb.IsChecked == true) selected.Add(c.Id); else selected.Remove(c.Id); Refresh(); };
            checks.Add(cb);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(6, 4) };
            row.Children.Add(new AvatarControl { Width = 32, Height = 32, DisplayName = c.Name, Image = c.Avatar, Status = c.Status });
            var name = new TextBlock { Text = c.Name, Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(cb, 2);
            row.Children.Add(name);
            row.Children.Add(cb);
            var item = new Border { Child = row, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent };
            item.PointerPressed += (_, _) => cb.IsChecked = cb.IsChecked != true;
            list.Children.Add(item);
        }
        var all = TextButton(Loc.T("Select all"), () => { foreach (var cb in checks) cb.IsChecked = true; });
        var none = TextButton(Loc.T("Select none"), () => { foreach (var cb in checks) cb.IsChecked = false; });
        all.MinWidth = 0; none.MinWidth = 0;
        send.Click += (_, _) =>
        {
            var t = box.Text?.Trim();
            if (!string.IsNullOrEmpty(t)) state.Broadcast(t, selected.ToList());
            w.Close();
        };
        box.TextChanged += (_, _) => Refresh();
        Refresh();

        var dock = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock { Text = w.Title, FontSize = 18, FontWeight = FontWeight.SemiBold });
        header.Children.Add(info);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { all, none } });
        DockPanel.SetDock(header, Dock.Top);
        var bottom = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        bottom.Children.Add(box);
        bottom.Children.Add(ButtonRow(TextButton(Loc.T("Cancel"), w.Close), send));
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(header);
        dock.Children.Add(bottom);
        dock.Children.Add(people.Count == 0
            ? new TextBlock { Text = Loc.T("Nobody is online to receive it."), Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new ScrollViewer { Content = list });
        w.Content = dock;
        return w.ShowDialog(owner);
    }

    // ---- chat room ---------------------------------------------------------------------------------

    public static Task NewGroup(Window owner, AppState state, RoomVm? inviteTo)
    {
        var w = Make(Loc.T(inviteTo == null ? "New Chat Room" : "Invite people"), 400, 520);
        var already = inviteTo?.Participants.ToHashSet() ?? new HashSet<string>();
        var candidates = state.Contacts.Where(c => !Proto.IsOffline(c.Status) && !already.Contains(c.Id)).ToList();
        var selected = new HashSet<string>();
        var list = new StackPanel { Spacing = 2 };
        foreach (var c in candidates)
        {
            var cb = new CheckBox { Content = null, VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
            cb.IsCheckedChanged += (_, _) => { if (cb.IsChecked == true) selected.Add(c.Id); else selected.Remove(c.Id); };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(6, 6) };
            row.Children.Add(new AvatarControl { Width = 36, Height = 36, DisplayName = c.Name, Image = c.Avatar, Status = c.Status });
            var name = new TextBlock { Text = c.Name, Margin = new Thickness(12, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(cb, 2);
            row.Children.Add(name);
            row.Children.Add(cb);
            var item = new Border { Child = row, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent };
            item.PointerPressed += (_, _) => cb.IsChecked = cb.IsChecked != true;
            list.Children.Add(item);
        }
        var go = TextButton(Loc.T(inviteTo == null ? "Create chat room" : "Invite"), () =>
        {
            if (selected.Count == 0) return;
            if (inviteTo == null)
            {
                var room = state.CreateRoom(selected);
                if (room != null) state.SelectRoom(room);
            }
            else state.InviteToRoom(inviteTo, selected);
            w.Close();
        }, accent: true);
        var dock = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = w.Title, FontSize = 18, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock { Text = Loc.T("Only people who are online right now can be added."), Opacity = 0.7 });
        DockPanel.SetDock(header, Dock.Top);
        var buttons = ButtonRow(TextButton(Loc.T("Cancel"), w.Close), go);
        buttons.Margin = new Thickness(0, 12, 0, 0);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(header);
        dock.Children.Add(buttons);
        dock.Children.Add(candidates.Count == 0
            ? new TextBlock { Text = Loc.T("Nobody to add."), Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new ScrollViewer { Content = list });
        go.IsEnabled = candidates.Count > 0;
        w.Content = dock;
        return w.ShowDialog(owner);
    }

    // ---- information about a person ----------------------------------------------------------------

    public static Task UserInfo(Window owner, AppState state, ChatVm chat)
    {
        var w = Make(chat.Title, 440, 400);
        var body = new TextBlock { Text = Loc.F("Asking {0} for details…", chat.Title), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), IsVisible = false };
        void OnInfo(string id, IReadOnlyDictionary<string, string> info)
        {
            if (id != chat.Key) return;
            var rows = new (string, string)[]
            {
                (Loc.T("Status"), StatusText.Label(info.GetValueOrDefault("status", ""))),
                (Loc.T("Note"), info.GetValueOrDefault("note", "")),
                (Loc.T("Logon name"), info.GetValueOrDefault("logon", "")),
                (Loc.T("Computer"), info.GetValueOrDefault("host", "")),
                (Loc.T("System"), info.GetValueOrDefault("os", "")),
                (Loc.T("Address"), info.GetValueOrDefault("address", "")),
                (Loc.T("Version"), info.GetValueOrDefault("version", "")),
                (Loc.T("Name"), string.Join(" ", new[] { info.GetValueOrDefault("firstname", ""), info.GetValueOrDefault("lastname", "") }.Where(s => s.Length > 0 && s != "N/A"))),
                (Loc.T("About"), info.GetValueOrDefault("about", "")),
            }.Where(r => r.Item2.Length > 0 && r.Item2 != "N/A").ToList();
            grid.RowDefinitions.Clear();
            grid.Children.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var k = new TextBlock { Text = rows[i].Item1, Opacity = 0.65, Margin = new Thickness(0, 3, 14, 3) };
                var v = new SelectableTextBlock { Text = rows[i].Item2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3) };
                Grid.SetRow(k, i); Grid.SetRow(v, i); Grid.SetColumn(v, 1);
                grid.Children.Add(k); grid.Children.Add(v);
            }
            body.IsVisible = false;
            grid.IsVisible = true;
        }
        state.InfoReceived += OnInfo;
        w.Closed += (_, _) => state.InfoReceived -= OnInfo;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            Children =
            {
                new AvatarControl { Width = 56, Height = 56, DisplayName = chat.Title, Image = chat.Contact.Avatar, Status = chat.Contact.Status },
                new TextBlock { Text = chat.Title, FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
            },
        });
        panel.Children.Add(body);
        panel.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 220 });
        panel.Children.Add(ButtonRow(TextButton(Loc.T("Close"), w.Close)));
        w.Content = panel;
        state.Messenger?.RequestUserInfo(chat.Key);
        return w.ShowDialog(owner);
    }

    // ---- history -----------------------------------------------------------------------------------

    public static Task History(Window owner, AppState state)
    {
        var w = Make(Loc.T("History"), 780, 560);
        var convos = state.History.Conversations();
        var list = new ListBox { ItemsSource = convos, Width = 260 };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<HistoryStore.Conversation>((c, _) =>
            new StackPanel
            {
                Margin = new Thickness(6, 4),
                Children =
                {
                    new TextBlock { Text = c.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock
                    {
                        Text = Loc.F("{0} messages · {1}", c.Count, DateTimeOffset.FromUnixTimeMilliseconds(c.LastTime).ToLocalTime().ToString("g")),
                        FontSize = 12, Opacity = 0.65,
                    },
                },
            });
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8), Text = Loc.T(convos.Count == 0 ? "Nothing saved yet" : "Select a conversation") };
        var messages = new StackPanel { Spacing = 8 };
        var delete = TextButton(Loc.T("Delete this conversation"), () => { }, false);
        delete.IsVisible = false;
        string? key = null;
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not HistoryStore.Conversation c) return;
            key = c.Key;
            title.Text = c.Title;
            delete.IsVisible = true;
            messages.Children.Clear();
            foreach (var e in state.History.Read(c.Key))
            {
                var head = new TextBlock
                {
                    Text = $"{e.Sender} · {DateTimeOffset.FromUnixTimeMilliseconds(e.Time).ToLocalTime():g}",
                    FontSize = 12, FontWeight = FontWeight.SemiBold, Opacity = e.Outgoing ? 1 : 0.8,
                };
                var text = new SelectableTextBlock { Text = e.Text, TextWrapping = TextWrapping.Wrap };
                messages.Children.Add(new StackPanel { Spacing = 1, Children = { head, text } });
            }
        };
        delete.Click += (_, _) =>
        {
            if (key == null) return;
            state.History.Delete(key);
            w.Close();
        };
        var clear = TextButton(Loc.T("Clear Message History"), () => { state.History.Clear(); w.Close(); });
        var right = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel { Orientation = Orientation.Vertical };
        top.Children.Add(title);
        DockPanel.SetDock(top, Dock.Top);
        var bottom = ButtonRow(delete, clear);
        bottom.Margin = new Thickness(0, 10, 0, 0);
        DockPanel.SetDock(bottom, Dock.Bottom);
        right.Children.Add(top);
        right.Children.Add(bottom);
        right.Children.Add(new ScrollViewer { Content = messages });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(list);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        w.Content = grid;
        return w.ShowDialog(owner);
    }

    // ---- about / help ------------------------------------------------------------------------------

    private static string Asset(string name)
    {
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/text/{name}"));
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch (Exception) { return ""; }
    }

    public static Task About(Window owner)
    {
        var w = Make(Loc.T("About"), 560, 560);
        var tabs = new TabControl();
        var head = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        head.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 16,
            Children =
            {
                new Image { Width = 72, Height = 72, Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://LanMessengerX/Assets/app-256.png"))) },
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "LAN Messenger X", FontSize = 22, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = Loc.F("Version {0}", Proto.Version), Opacity = 0.7 },
                    },
                },
            },
        });
        head.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = Loc.T("A free peer-to-peer messenger for local networks. It needs no server and speaks the protocol of the original LAN Messenger, so it works together with the older clients.") });
        head.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Text = "Original LAN Messenger: Copyright © 2010-2016 Qualia Digital Solutions (Dilip Radhakrishnan). Continued by galandras12. GNU General Public License, version 3." });
        tabs.Items.Add(new TabItem { Header = Loc.T("About"), Content = new ScrollViewer { Content = head } });
        tabs.Items.Add(new TabItem { Header = Loc.T("Thanks"), Content = new ScrollViewer { Content = new SelectableTextBlock { Text = Asset("thanks.txt"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) } } });
        tabs.Items.Add(new TabItem { Header = Loc.T("License"), Content = new ScrollViewer { Content = new SelectableTextBlock { Text = Asset("license.txt"), TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas,Menlo,monospace"), FontSize = 11, Margin = new Thickness(16) } } });
        var dock = new DockPanel { Margin = new Thickness(12) };
        var close = ButtonRow(TextButton(Loc.T("Close"), w.Close));
        close.Margin = new Thickness(0, 10, 0, 0);
        DockPanel.SetDock(close, Dock.Bottom);
        dock.Children.Add(close);
        dock.Children.Add(tabs);
        w.Content = dock;
        return w.ShowDialog(owner);
    }

    public static Task Help(Window owner)
    {
        var w = Make(Loc.T("Help"), 600, 560);
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        void H(string t) => panel.Children.Add(new TextBlock { Text = Loc.T(t), FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        void P(string t) => panel.Children.Add(new TextBlock { Text = Loc.T(t), TextWrapping = TextWrapping.Wrap, Opacity = 0.9 });
        H("Finding people");
        P("LAN Messenger X finds the people on your local network by itself - there is no server and no account. If someone is missing, use Refresh Contacts List, make sure you are on the same network, and that the firewall allows LAN Messenger X (Preferences > Network).");
        H("Chatting");
        P("Pick a person in the list and type. Enter sends the message (this can be changed in Preferences > Hotkeys). You can see when the other side is typing and when your message was delivered.");
        H("Your picture and status message");
        P("Click your picture to choose one of the pictures or one of your own. Under your name you can write a status message that everybody sees next to your name; the arrow next to your status changes it between Available, Busy, Do not disturb, Be right back, Away and Appear offline.");
        H("Files and folders");
        P("Use the paperclip in a conversation or the right-click menu of a person to send files or a whole folder. Received files are saved in the folder set in Preferences > File Transfer. All transfers are listed in Tools > File Transfers.");
        H("Chat rooms, Public chat and Broadcast");
        P("A chat room is a conversation between several people you invite. The public chat is seen by everyone who has it open. A broadcast message is a one-off message to the people you select.");
        H("Groups in the list");
        P("Right-click the list to add groups and move people between them, for example by department.");
        panel.Children.Add(ButtonRow(TextButton(Loc.T("Close"), w.Close)));
        w.Content = new ScrollViewer { Content = panel };
        return w.ShowDialog(owner);
    }
}
