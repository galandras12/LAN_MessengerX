using Avalonia.Controls;
using Avalonia.Interactivity;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;

namespace LanMessengerX.App.Views;

public partial class MainWindow : Window
{
    private AppState State => (AppState)DataContext!;
    private bool _syncing;

    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) =>
        {
            if (DataContext is AppState s)
            {
                s.WindowActive = true;
                if (s.Selected is ChatVm c) c.Contact.Unread = 0;
                else if (s.Selected is RoomVm r) r.Unread = 0;
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
            };
            s.Contacts.CollectionChanged += (_, _) => UpdateEmpty();
            s.Rooms.CollectionChanged += (_, _) => UpdateEmpty();
            s.ItemAdded += conv => { if (s.Selected == conv) Chat.ScrollToEnd(); };
            UpdateEmpty();
            SyncSelection();
        };
    }

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
        }
        finally { _syncing = false; }
    }

    private void OnPeopleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (PeopleList.SelectedItem is ContactVm c) State.SelectChat(c);
    }

    private void OnRoomSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (RoomList.SelectedItem is RoomVm r) State.SelectRoom(r);
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => State.Refresh();
    private async void OnSettings(object? sender, RoutedEventArgs e) => await new SettingsWindow { DataContext = State }.ShowDialog(this);
    private async void OnBroadcast(object? sender, RoutedEventArgs e) => await Dialogs.Broadcast(this, State);
    private async void OnNewGroup(object? sender, RoutedEventArgs e) => await Dialogs.NewGroup(this, State, null);
    private async void OnHistory(object? sender, RoutedEventArgs e) => await Dialogs.History(this, State);
}
