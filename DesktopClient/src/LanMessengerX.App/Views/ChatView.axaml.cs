using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;

namespace LanMessengerX.App.Views;

public partial class ChatView : UserControl
{
    private ConversationVm? _vm;

    private void ApplyTextSize()
    {
        if (AppState.Current == null) return;
        Scroll.FontSize = AppState.Current.Settings.TextSize * 96.0 / 72.0;
        Input.FontSize = Scroll.FontSize;
    }

    public ChatView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyTextSize();
            if (AppState.Current != null) AppState.Current.SettingsApplied += () => { ApplyTextSize(); _vm?.RaiseHeader(); };
        };
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.Items.CollectionChanged -= OnItemsChanged;
            _vm = DataContext as ConversationVm;
            if (_vm != null) _vm.Items.CollectionChanged += OnItemsChanged;
            InviteButton.IsVisible = LeaveButton.IsVisible = _vm is RoomVm;
            InfoButton.IsVisible = AttachPanel.IsVisible = _vm is ChatVm;
            ScrollToEnd();
            Dispatcher.UIThread.Post(() => Input.Focus(), DispatcherPriority.Background);
        };
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    public void ScrollToEnd() => Dispatcher.UIThread.Post(() => Scroll.ScrollToEnd(), DispatcherPriority.Background);

    public void FocusInput() => Input.Focus();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm == null) return;
        bool sendWithEnter = AppState.Current?.Settings.SendWithEnter ?? true;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift), ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool send = sendWithEnter ? !shift && !ctrl : ctrl && !shift;
        if (!send)
        {
            if (!sendWithEnter && !shift && !ctrl) return; // plain Enter: new line
            if (ctrl)
            {
                // Ctrl+Enter inserts a line break when Enter is the send key
                e.Handled = true;
                Input.SelectedText = "\n";
                Input.CaretIndex = Input.SelectionEnd;
            }
            return;
        }
        e.Handled = true;
        if (_vm.SendCommand.CanExecute(null)) _vm.SendCommand.Execute(null);
    }

    private async void OnAttach(object? sender, RoutedEventArgs e)
    {
        if (_vm is not ChatVm chat || TopLevel.GetTopLevel(this) is not Window w) return;
        var files = await Pickers.OpenFiles(w, Loc.T("Send files"), multiple: true);
        foreach (var f in files) AppState.Current.SendFile(chat, f);
    }

    private async void OnSendFolder(object? sender, RoutedEventArgs e)
    {
        if (_vm is not ChatVm chat || TopLevel.GetTopLevel(this) is not Window w) return;
        var dir = await Pickers.OpenFolder(w, Loc.T("Send Folder"));
        if (dir != null) AppState.Current.SendFolder(chat, dir);
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
