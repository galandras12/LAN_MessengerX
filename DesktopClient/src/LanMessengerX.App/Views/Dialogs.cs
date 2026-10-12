using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanMessengerX.App.Controls;
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
    };

    private static Button TextButton(string label, Action click, bool accent = false)
    {
        var b = new Button { Content = label, MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (accent) b.Classes.Add("accent");
        b.Padding = new Thickness(16, 7);
        b.Click += (_, _) => click();
        return b;
    }

    public static Task Broadcast(Window owner, AppState state)
    {
        var w = Make("Broadcast message", 440, 280);
        var online = state.Contacts.Count(c => !Proto.IsOffline(c.Status));
        var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Watermark = "Message to everyone online" };
        var info = new TextBlock
        {
            Text = online == 0 ? "Nobody is online to receive it." : $"Sent to {online} {(online == 1 ? "person" : "people")} who are online right now.",
            Opacity = 0.7, TextWrapping = TextWrapping.Wrap,
        };
        var send = TextButton("Send", () =>
        {
            var t = box.Text?.Trim();
            if (!string.IsNullOrEmpty(t) && online > 0) state.Broadcast(t);
            w.Close();
        }, accent: true);
        send.IsEnabled = online > 0;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Broadcast message", FontSize = 18, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(info);
        panel.Children.Add(box);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8,
            Children = { TextButton("Cancel", w.Close), send },
        });
        w.Content = panel;
        return w.ShowDialog(owner);
    }

    public static Task NewGroup(Window owner, AppState state, RoomVm? inviteTo)
    {
        var w = Make(inviteTo == null ? "New group chat" : "Invite people", 400, 520);
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
        var go = TextButton(inviteTo == null ? "Create group" : "Invite", () =>
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
        header.Children.Add(new TextBlock { Text = "Only people who are online right now can be added.", Opacity = 0.7 });
        DockPanel.SetDock(header, Dock.Top);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 12, 0, 0),
            Children = { TextButton("Cancel", w.Close), go },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(header);
        dock.Children.Add(buttons);
        dock.Children.Add(candidates.Count == 0
            ? new TextBlock { Text = "Nobody to add.", Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new ScrollViewer { Content = list });
        go.IsEnabled = candidates.Count > 0;
        w.Content = dock;
        return w.ShowDialog(owner);
    }

    public static Task UserInfo(Window owner, AppState state, ChatVm chat)
    {
        var w = Make(chat.Title, 420, 360);
        var body = new TextBlock { Text = $"Asking {chat.Title} for details…", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        void OnInfo(string id, IReadOnlyDictionary<string, string> info)
        {
            if (id != chat.Key) return;
            var rows = new (string, string)[]
            {
                ("Status", StatusText.Label(info.GetValueOrDefault("status", ""))),
                ("Note", info.GetValueOrDefault("note", "")),
                ("Logon name", info.GetValueOrDefault("logon", "")),
                ("Computer", info.GetValueOrDefault("host", "")),
                ("System", info.GetValueOrDefault("os", "")),
                ("Address", info.GetValueOrDefault("address", "")),
                ("Version", info.GetValueOrDefault("version", "")),
                ("Name", string.Join(" ", new[] { info.GetValueOrDefault("firstname", ""), info.GetValueOrDefault("lastname", "") }.Where(s => s.Length > 0 && s != "N/A"))),
                ("About", info.GetValueOrDefault("about", "")),
            };
            body.Text = string.Join("\n", rows.Where(r => r.Item2.Length > 0 && r.Item2 != "N/A").Select(r => $"{r.Item1}: {r.Item2}"));
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
        panel.Children.Add(new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { TextButton("Close", w.Close) } });
        w.Content = panel;
        state.Messenger?.RequestUserInfo(chat.Key);
        return w.ShowDialog(owner);
    }

    public static Task History(Window owner, AppState state)
    {
        var w = Make("History", 760, 540);
        var convos = state.History.Conversations();
        var list = new ListBox { ItemsSource = convos, Width = 250 };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<HistoryStore.Conversation>((c, _) =>
            new StackPanel
            {
                Margin = new Thickness(6, 4),
                Children =
                {
                    new TextBlock { Text = c.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock
                    {
                        Text = $"{c.Count} messages · {DateTimeOffset.FromUnixTimeMilliseconds(c.LastTime).ToLocalTime():g}",
                        FontSize = 12, Opacity = 0.65,
                    },
                },
            });
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8), Text = convos.Count == 0 ? "Nothing saved yet" : "Select a conversation" };
        var messages = new StackPanel { Spacing = 8 };
        var delete = TextButton("Delete this conversation", () => { }, false);
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
        var clear = TextButton("Clear all", () => { state.History.Clear(); w.Close(); });
        var right = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel { Orientation = Orientation.Vertical };
        top.Children.Add(title);
        DockPanel.SetDock(top, Dock.Top);
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0), Children = { delete, clear } };
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
}
