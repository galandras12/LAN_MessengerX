using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Views;

public partial class MainWindow : Window
{
    private AppState State => (AppState)DataContext!;
    private bool _syncing;
    private TransfersWindow? _transfers;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => OfferFirewall();
        Activated += (_, _) =>
        {
            if (DataContext is AppState s)
            {
                s.WindowActive = true;
                if (s.Selected is ChatVm c) c.Contact.Unread = 0;
                else if (s.Selected is RoomVm r) r.Unread = 0;
                else if (s.Selected is PublicVm pv) pv.Unread = 0;
                UpdateTitle();
            }
        };
        Deactivated += (_, _) => { if (DataContext is AppState s) s.WindowActive = false; };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not AppState s) return;
            s.UnreadChanged += UpdateTitle;
            s.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppState.Selected)) { SyncSelection(); UpdateTitle(); }
                if (e.PropertyName == nameof(AppState.TotalUnread)) UpdateTitle();
                if (e.PropertyName == nameof(AppState.Status)) RefreshStatusUi();
                if (e.PropertyName == nameof(AppState.Note) && !NoteBox.IsKeyboardFocusWithin) NoteBox.Text = s.Note;
            };
            s.Contacts.CollectionChanged += (_, _) => UpdateEmpty();
            s.Rooms.CollectionChanged += (_, _) => UpdateEmpty();
            s.RosterChanged += SyncSelection;
            s.ItemAdded += conv => { if (s.Selected == conv) Chat.ScrollToEnd(); };
            s.TransferStarted += () => OpenTransfers(activate: false);
            BuildStockGrid();
            BuildStatusUi();
            FlowDirection = Loc.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
            Loc.Instance.PropertyChanged += (_, _) =>
            {
                FlowDirection = Loc.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
                BuildStatusUi(); UpdateEmpty(); s.RebuildRoster();
            };
            NoteBox.Text = s.Note;
            NoteBox.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { CommitNote(); e.Handled = true; } };
            NoteBox.LostFocus += (_, _) => CommitNote();
            AvatarButton.Flyout!.Opened += (_, _) => PopupNote.Text = State.Note;
            UpdateEmpty();
            SyncSelection();
        };
    }

    /// <summary>Windows only, once: files and chat only reach this computer if the firewall lets the program in.</summary>
    private async void OfferFirewall()
    {
        if (!Firewall.Supported || State.Settings.FirewallAsked) return;
        State.Settings.FirewallAsked = true;
        State.Settings.Save();
        await Task.Delay(1500);
        var yes = await Dialogs.Confirm(this, Loc.T("Windows Firewall"),
            Loc.T("For other people to reach this computer (messages, files), the Windows Firewall has to allow LAN Messenger X. Allow it now? Windows asks for administrator permission once."),
            Loc.T("Allow"));
        if (!yes) return;
        bool ok = await Task.Run(() => Firewall.Allow(out _));
        if (ok) State.Messenger?.Refresh();
    }

    // ---- profile: picture, status, status message -------------------------------------------------

    private void BuildStockGrid()
    {
        StockGrid.Children.Clear();
        for (int i = 0; i < Avatars.StockCount; i++)
        {
            int index = i;
            var img = new Image { Source = State.StockAvatars[i], Width = 48, Height = 48, Stretch = Stretch.UniformToFill };
            var b = new Button { Content = img, Padding = new Thickness(0), Margin = new Thickness(2), Background = Brushes.Transparent, CornerRadius = new CornerRadius(8), ClipToBounds = true };
            b.Click += (_, _) =>
            {
                Avatars.UseStock(State, index);
                AvatarButton.Flyout?.Hide();
            };
            StockGrid.Children.Add(b);
        }
    }

    private void BuildStatusUi()
    {
        var items = new List<MenuItem>();
        foreach (var code in Proto.StatusCodes)
        {
            var c = code;
            var mi = new MenuItem { Header = StatusText.Label(c), Icon = new Ellipse { Width = 10, Height = 10, Fill = StatusText.Presence(c) } };
            mi.Click += (_, _) => State.SetStatus(c);
            items.Add(mi);
        }
        StatusButton.Flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft, ItemsSource = items };
        PopupStatus.ItemsSource = Proto.StatusCodes.Select(c => new StatusChoice(c)).ToList();
        PopupStatus.SelectedIndex = Math.Max(0, Array.IndexOf(Proto.StatusCodes, State.Status));
        RefreshStatusUi();
    }

    private void RefreshStatusUi()
    {
        StatusLabel.Text = StatusText.Label(State.Status);
        var idx = Array.IndexOf(Proto.StatusCodes, State.Status);
        if (idx >= 0 && PopupStatus.SelectedIndex != idx) PopupStatus.SelectedIndex = idx;
    }

    private void CommitNote()
    {
        var text = NoteBox.Text ?? "";
        if (text.Trim() != State.Note) State.SetNote(text);
        // let go of the keyboard so that typing goes to the conversation again
        if (NoteBox.IsKeyboardFocusWithin) Chat.FocusInput();
    }

    private void OnShareNote(object? sender, RoutedEventArgs e)
    {
        State.SetNote(PopupNote.Text ?? "");
        NoteBox.Text = State.Note;
        if (PopupStatus.SelectedItem is StatusChoice sc && sc.Code != State.Status) State.SetStatus(sc.Code);
        AvatarButton.Flyout?.Hide();
    }

    private async void OnBrowsePicture(object? sender, RoutedEventArgs e)
    {
        AvatarButton.Flyout?.Hide();
        var files = await Pickers.OpenFiles(this, Loc.T("Select avatar picture"), multiple: false, imagesOnly: true);
        if (files.Count == 0) return;
        var error = Avatars.UseFile(State, files[0]);
        if (error != null) await Dialogs.Message(this, Loc.T("Select avatar picture"), Loc.F("This picture could not be used: {0}", error));
    }

    private void OnRemovePicture(object? sender, RoutedEventArgs e)
    {
        State.Settings.StockAvatar = -1;
        State.SetAvatar(null);
        AvatarButton.Flyout?.Hide();
    }

    // ---- lists --------------------------------------------------------------------------------------

    private void UpdateEmpty()
    {
        PeopleEmpty.IsVisible = State.Contacts.Count == 0;
        RoomsEmpty.IsVisible = State.Rooms.Count == 0;
    }

    private void UpdateTitle()
    {
        int n = State.TotalUnread;
        Title = n > 0 ? $"({n}) LAN Messenger X" : "LAN Messenger X";
    }

    private void SyncSelection()
    {
        _syncing = true;
        try
        {
            if (State.Selected is ChatVm c) { PeopleList.SelectedItem = c.Contact; RoomList.SelectedItem = null; }
            else if (State.Selected is RoomVm r) { RoomList.SelectedItem = r; PeopleList.SelectedItem = null; }
            else { PeopleList.SelectedItem = null; RoomList.SelectedItem = null; }
            PublicButton.Background = State.Selected is PublicVm ? new SolidColorBrush(Color.Parse("#33808080")) : Brushes.Transparent;
        }
        finally { _syncing = false; }
    }

    private void OnPeopleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (PeopleList.SelectedItem is ContactVm c) State.SelectChat(c);
    }

    private void OnGroupTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is GroupRowVm g) State.ToggleGroup(g);
    }

    private void OnRoomSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (RoomList.SelectedItem is RoomVm r) State.SelectRoom(r);
    }

    private void OnPublicChat(object? sender, RoutedEventArgs e) => State.Selected = State.PublicChat;

    private void OnRosterContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var item = (Control)e.Container;
        var row = item.DataContext;
        if (row is ContactVm c) item.ContextMenu = ContactMenu(c);
        else if (row is GroupRowVm g) item.ContextMenu = GroupMenu(g);
    }

    private ContextMenu ContactMenu(ContactVm c)
    {
        var menu = new ContextMenu();
        MenuItem Item(string text, Action act)
        {
            var mi = new MenuItem { Header = Loc.T(text) };
            mi.Click += (_, _) => act();
            return mi;
        }
        menu.Items.Add(Item("Conversation", () => State.SelectChat(c)));
        menu.Items.Add(Item("Send Broadcast Message", async () => await Dialogs.Broadcast(this, State, c.Id)));
        menu.Items.Add(Item("Send File", async () =>
        {
            var chat = State.ChatOf(c);
            State.SelectChat(c);
            var files = await Pickers.OpenFiles(this, Loc.T("Send files"), multiple: true);
            foreach (var f in files) State.SendFile(chat, f);
        }));
        menu.Items.Add(Item("Send Folder", async () =>
        {
            var chat = State.ChatOf(c);
            State.SelectChat(c);
            var dir = await Pickers.OpenFolder(this, Loc.T("Send Folder"));
            if (dir != null) State.SendFolder(chat, dir);
        }));
        menu.Items.Add(Item("Get Information", async () => await Dialogs.UserInfo(this, State, State.ChatOf(c))));
        if (State.Groups.Count > 0)
        {
            menu.Items.Add(new Separator());
            var move = new MenuItem { Header = Loc.T("Move to group") };
            var targets = new List<MenuItem>();
            foreach (var (id, name) in State.Groups.Select(g => (g.Id, g.Name)).Prepend(("", Loc.T("General"))))
            {
                var gid = id;
                var mi = new MenuItem { Header = name, IsEnabled = c.GroupId != gid };
                mi.Click += (_, _) => State.MoveContact(c, gid);
                targets.Add(mi);
            }
            move.ItemsSource = targets;
            menu.Items.Add(move);
        }
        return menu;
    }

    private ContextMenu GroupMenu(GroupRowVm g)
    {
        var menu = new ContextMenu();
        MenuItem Item(string text, Action act, bool enabled = true)
        {
            var mi = new MenuItem { Header = Loc.T(text), IsEnabled = enabled };
            mi.Click += (_, _) => act();
            return mi;
        }
        menu.Items.Add(Item("Add New Group", AddGroup));
        menu.Items.Add(Item("Rename This Group", async () =>
        {
            var name = await Dialogs.Prompt(this, Loc.T("Rename Group"), Loc.T("Enter a new name for the group"), g.Name);
            if (name == null) return;
            if (State.GroupExists(name) && !string.Equals(name.Trim(), g.Name, StringComparison.CurrentCultureIgnoreCase))
            { await Dialogs.Message(this, Loc.T("Rename Group"), Loc.F("A group named '{0}' already exists. Please enter a different name.", name.Trim())); return; }
            State.RenameGroup(g.Id, name);
        }, !g.IsDefault));
        menu.Items.Add(Item("Delete This Group", () => State.DeleteGroup(g.Id), !g.IsDefault));
        return menu;
    }

    private async void AddGroup()
    {
        var name = await Dialogs.Prompt(this, Loc.T("Add New Group"), Loc.T("Enter a name for the group"), "");
        if (name == null || name.Trim().Length == 0) return;
        if (State.GroupExists(name)) { await Dialogs.Message(this, Loc.T("Add New Group"), Loc.F("A group named '{0}' already exists. Please enter a different name.", name.Trim())); return; }
        State.AddGroup(name);
    }

    // ---- menu / buttons -----------------------------------------------------------------------------

    private void OnRefresh(object? sender, RoutedEventArgs e) => State.Refresh();
    private void OnExit(object? sender, RoutedEventArgs e) => (Application.Current as App)?.Quit();
    private void OnSettings(object? sender, RoutedEventArgs e) => OpenSettings();
    private async void OnBroadcast(object? sender, RoutedEventArgs e) => await Dialogs.Broadcast(this, State, null);
    private async void OnNewGroup(object? sender, RoutedEventArgs e) => await Dialogs.NewGroup(this, State, null);
    private void OnHistory(object? sender, RoutedEventArgs e) => OpenHistory();
    private void OnTransfers(object? sender, RoutedEventArgs e) => OpenTransfers();
    private void OnAbout(object? sender, RoutedEventArgs e) => OpenAbout();
    private async void OnHelp(object? sender, RoutedEventArgs e) => await Dialogs.Help(this);
    private void OnUpdates(object? sender, RoutedEventArgs e) => Shell.Open("https://github.com/galandras12/LAN_MessengerX/releases");

    public async void OpenSettings() => await new SettingsWindow { DataContext = State }.ShowDialog(this);
    public async void OpenHistory() => await Dialogs.History(this, State);
    public async void OpenAbout() => await Dialogs.About(this);

    public void OpenTransfers(bool activate = true)
    {
        if (_transfers == null || !_transfers.IsLoaded)
        {
            _transfers = new TransfersWindow { DataContext = State };
            _transfers.Closed += (_, _) => _transfers = null;
            _transfers.Show();
        }
        else if (activate) _transfers.Activate();
    }
}
