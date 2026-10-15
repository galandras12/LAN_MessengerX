using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LanMessengerX.Core;

namespace LanMessengerX.App.ViewModels;

/// <summary>Shared by one-to-one chats and group rooms: message list, draft and send command.</summary>
public abstract class ConversationVm : Observable
{
    protected readonly Services.AppState App;
    public ObservableCollection<ChatItemVm> Items { get; } = new();
    private string _draft = "";
    private readonly DispatcherTimer _pauseTimer;
    private bool _composing;

    protected ConversationVm(Services.AppState app)
    {
        App = app;
        SendCommand = new RelayCommand(Send, () => !string.IsNullOrWhiteSpace(_draft));
        _pauseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _pauseTimer.Tick += (_, _) =>
        {
            _pauseTimer.Stop();
            if (_composing) { _composing = false; OnTyping(false); }
        };
    }

    public abstract string Key { get; }
    public abstract string Title { get; }
    public abstract string Subtitle { get; }
    public virtual bool IsGroup => false;
    public virtual bool CanAttach => false;
    public virtual Bitmap? AvatarImage => null;
    public virtual string? AvatarStatus => null;

    public ICommand SendCommand { get; }

    /// <summary>Watermark of the input box (depends on the send key and the language).</summary>
    public string Hint => Localization.Loc.T(Services.AppState.Current?.Settings.SendWithEnter == false
        ? "Type a message - Ctrl+Enter to send, Enter for a new line"
        : "Type a message - Enter to send, Shift+Enter for a new line");

    public string Draft
    {
        get => _draft;
        set
        {
            if (!Set(ref _draft, value)) return;
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!_composing) { _composing = true; OnTyping(true); }
                _pauseTimer.Stop();
                _pauseTimer.Start();
            }
        }
    }

    private void Send()
    {
        var t = _draft.Trim();
        if (t.Length == 0) return;
        Draft = "";
        _pauseTimer.Stop();
        if (_composing) { _composing = false; OnTyping(false); }
        SendText(t);
    }

    protected abstract void SendText(string text);
    protected virtual void OnTyping(bool composing) { }

    public void AddItem(ChatItemVm item)
    {
        var prev = Items.LastOrDefault();
        item.ShowDay = prev == null || DateTimeOffset.FromUnixTimeMilliseconds(prev.Time).ToLocalTime().Date != DateTimeOffset.FromUnixTimeMilliseconds(item.Time).ToLocalTime().Date;
        Items.Add(item);
    }

    public void RaiseHeader()
    {
        Raise(nameof(Title));
        Raise(nameof(Subtitle));
        Raise(nameof(AvatarImage));
        Raise(nameof(AvatarStatus));
    }
}

public sealed class ChatVm : ConversationVm
{
    public ContactVm Contact { get; }
    public bool Online { get; set; }

    public ChatVm(Services.AppState app, ContactVm contact) : base(app)
    {
        Contact = contact;
        contact.PropertyChanged += (_, _) => RaiseHeader();
    }

    public override string Key => Contact.Id;
    public override string Title => Contact.Name;
    public override string Subtitle => Online ? Contact.Subtitle : Localization.Loc.T("Offline");
    public override bool CanAttach => Online;
    public override Bitmap? AvatarImage => Contact.Avatar;
    public override string? AvatarStatus => Online ? Contact.Status : "gone";

    protected override void SendText(string text)
    {
        if (!Online)
        {
            AddItem(new NoticeVm(Localization.Loc.F("{0} is offline - message not sent", Title), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            return;
        }
        App.SendText(this, text);
    }

    protected override void OnTyping(bool composing)
    {
        if (Online) App.Messenger?.SendChatState(Contact.Id, composing ? ChatStateKind.Composing : ChatStateKind.Paused);
    }
}

public sealed class RoomVm : ConversationVm
{
    public string ThreadId { get; }
    public List<string> Participants { get; set; } = new();
    public int Unread { get; set; }
    public long LastTime { get; set; }

    public RoomVm(Services.AppState app, string threadId) : base(app) => ThreadId = threadId;

    public override string Key => ThreadId;
    public override bool IsGroup => true;
    public override string Title => App.RoomTitle(this);
    public override string Subtitle => Localization.Loc.F("{0} participants", Participants.Count);

    public string LastText => (Items.LastOrDefault(i => i is MessageVm) as MessageVm) is { } m ? $"{m.Sender}: {m.Text}" : Subtitle;

    protected override void SendText(string text) => App.SendRoomText(this, text);
}

/// <summary>The public chat: everybody on the network who has it open sees what is written here.</summary>
public sealed class PublicVm : ConversationVm
{
    public const string KeyValue = "public";
    private int _unread;
    public int Unread { get => _unread; set { if (Set(ref _unread, value)) { Raise(nameof(HasUnread)); Raise(nameof(UnreadText)); } } }
    public bool HasUnread => _unread > 0;
    public string UnreadText => _unread > 99 ? "99+" : _unread.ToString();
    public PublicVm(Services.AppState app) : base(app) { }
    public override string Key => KeyValue;
    public override bool IsGroup => true;
    public override string Title => Localization.Loc.T("Public chat");
    public override string Subtitle => Localization.Loc.T("Everyone on the network");
    protected override void SendText(string text) => App.SendPublicText(this, text);
}
