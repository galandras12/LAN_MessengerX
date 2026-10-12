using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;

namespace LanMessengerX.App.Views;

public partial class ChatView : UserControl
{
    private ConversationVm? _vm;

    public ChatView()
    {
        InitializeComponent();
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.Items.CollectionChanged -= OnItemsChanged;
            _vm = DataContext as ConversationVm;
            if (_vm != null) _vm.Items.CollectionChanged += OnItemsChanged;
            InviteButton.IsVisible = LeaveButton.IsVisible = _vm is RoomVm;
            InfoButton.IsVisible = AttachButton.IsVisible = _vm is ChatVm;
            ScrollToEnd();
            Dispatcher.UIThread.Post(() => Input.Focus(), DispatcherPriority.Background);
        };
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    public void ScrollToEnd() => Dispatcher.UIThread.Post(() => Scroll.ScrollToEnd(), DispatcherPriority.Background);

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _vm != null)
        {
            e.Handled = true;
            if (_vm.SendCommand.CanExecute(null)) _vm.SendCommand.Execute(null);
        }
    }

    private async void OnAttach(object? sender, RoutedEventArgs e)
    {
        if (_vm is not ChatVm chat || TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Send files", AllowMultiple = true });
        foreach (var f in files)
            if (f.TryGetLocalPath() is { } p) AppState.Current.SendFile(chat, p);
    }

    private async void OnInfo(object? sender, RoutedEventArgs e)
    {
        if (_vm is ChatVm chat && TopLevel.GetTopLevel(this) is Window w) await Dialogs.UserInfo(w, AppState.Current, chat);
    }

    private async void OnInvite(object? sender, RoutedEventArgs e)
    {
        if (_vm is RoomVm room && TopLevel.GetTopLevel(this) is Window w) await Dialogs.NewGroup(w, AppState.Current, room);
    }

    private void OnLeave(object? sender, RoutedEventArgs e)
    {
        if (_vm is RoomVm room) AppState.Current.LeaveRoom(room);
    }
}
