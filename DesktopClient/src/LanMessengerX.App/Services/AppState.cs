using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Services;

/// <summary>Glue between the protocol engine and the view models. Lives on the UI thread.</summary>
public sealed class AppState : Observable
{
    public static AppState Current { get; private set; } = null!;

    public AppSettings Settings { get; }
    public HistoryStore History { get; }
    public Messenger? Messenger { get; private set; }
    public ObservableCollection<ContactVm> Contacts { get; } = new();
    public ObservableCollection<RoomVm> Rooms { get; } = new();
    private readonly Dictionary<string, ChatVm> _chats = new();
    private readonly Dictionary<string, ContactVm> _contactById = new();
    private readonly HashSet<string> _online = new();

    private ConversationVm? _selected;
    private string _connectionText = "Connecting…";
    private bool _connected;
    private string? _ip;
    private Bitmap? _myAvatar;
    private string _name = "";
    private string _note = "";
    private string _status = "chat";

    public bool WindowActive { get; set; }
    public event Action<ConversationVm>? ItemAdded;
    public event Action<string, string, string>? Notification; // title, text, conversation key

    public string AvatarFile => Path.Combine(AppSettings.DataDir, "avatar.png");
    public string CacheDir => Path.Combine(AppSettings.DataDir, "avatars");

    public AppState(AppSettings settings)
    {
        Current = this;
        Settings = settings;
        History = new HistoryStore(AppSettings.DataDir);
        Directory.CreateDirectory(CacheDir);
        if (string.IsNullOrEmpty(settings.MachineId))
        {
            settings.MachineId = NetUtil.FirstMac() ?? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6));
            settings.Save();
        }
        if (string.IsNullOrWhiteSpace(settings.Name)) settings.Name = Environment.UserName;
        _name = settings.Name;
        _note = settings.Note;
        _status = settings.Status;
        if (File.Exists(AvatarFile)) _myAvatar = LoadBitmap(AvatarFile);
    }

    // ---- profile / connection ---------------------------------------------------------------------

    public string Name { get => _name; private set => Set(ref _name, value); }
    public string Note { get => _note; private set => Set(ref _note, value); }
    public string Status { get => _status; private set { if (Set(ref _status, value)) { Raise(nameof(Presence)); } } }
    public Bitmap? MyAvatar { get => _myAvatar; private set => Set(ref _myAvatar, value); }
    public Avalonia.Media.IBrush Presence => StatusText.Presence(_status);
    public string ConnectionText { get => _connectionText; private set => Set(ref _connectionText, value); }
    public bool Connected { get => _connected; private set => Set(ref _connected, value); }
    public string? Ip { get => _ip; private set => Set(ref _ip, value); }
    public string LocalId => Messenger?.LocalId ?? "";

    public ConversationVm? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            if (value is ChatVm c) { c.Contact.Unread = 0; }
            if (value is RoomVm r) { r.Unread = 0; RoomsChanged(); }
            Raise(nameof(HasSelection));
            Raise(nameof(TotalUnread));
        }
    }

    public bool HasSelection => _selected != null;
    public int TotalUnread => Contacts.Sum(c => c.Unread) + Rooms.Sum(r => r.Unread);
    public event Action? UnreadChanged;

    // ---- lifecycle ----------------------------------------------------------------------------------

    public void Start()
    {
        var cfg = new MessengerConfig
        {
            UserName = Settings.Name,
            Status = Settings.Status,
            Note = Settings.Note,
            MachineId = Settings.MachineId,
            LogonName = Environment.UserName,
            HostName = Environment.MachineName,
            OsName = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            CacheDir = CacheDir,
            UdpPort = Settings.Port,
            TcpPort = Settings.Port,
            PreferredInterface = Settings.PreferredInterface,
            ExtraBroadcasts = Settings.ExtraBroadcasts.ToList(),
        };
        Messenger = new Messenger(cfg, s => System.Diagnostics.Debug.WriteLine("[lmx] " + s), e => Dispatcher.UIThread.Post(() => OnEvent(e)));
        Messenger.Start();
        if (File.Exists(AvatarFile)) Messenger.SetLocalAvatar(AvatarFile);
    }

    public void Shutdown()
    {
        Messenger?.Stop();
        Messenger = null;
        Settings.Save();
    }

    // ---- engine events ------------------------------------------------------------------------------

    private ContactVm ContactFor(string id, User? u = null)
    {
        if (!_contactById.TryGetValue(id, out var c))
        {
            c = new ContactVm(u ?? new User(id, "", "", id, "gone", "", 0));
            _contactById[id] = c;
        }
        else if (u != null) c.Apply(u);
        if (u?.AvatarPath != null && c.Avatar == null) c.Avatar = LoadBitmap(u.AvatarPath);
        return c;
    }

    private ChatVm ChatFor(string id, User? u = null)
    {
        var contact = ContactFor(id, u);
        if (!_chats.TryGetValue(id, out var chat))
        {
            chat = new ChatVm(this, contact) { Online = _online.Contains(id) };
            _chats[id] = chat;
        }
        return chat;
    }

    private void SortContacts()
    {
        var sorted = Contacts.OrderBy(c => Proto.IsOffline(c.Status)).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int cur = Contacts.IndexOf(sorted[i]);
            if (cur != i) Contacts.Move(cur, i);
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void OnEvent(MessengerEvent e)
    {
        switch (e)
        {
            case MessengerEvent.ConnectionChanged c:
                Connected = c.Connected;
                Ip = c.Ip;
                ConnectionText = c.Connected ? $"Online · {c.Ip}" : "Waiting for a network…";
                break;
            case MessengerEvent.UserOnline u:
            {
                var chat = ChatFor(u.User.Id, u.User);
                chat.Online = true;
                _online.Add(u.User.Id);
                if (!Contacts.Contains(chat.Contact)) Contacts.Add(chat.Contact);
                SortContacts();
                chat.RaiseHeader();
                break;
            }
            case MessengerEvent.UserUpdated u:
            {
                var chat = ChatFor(u.User.Id, u.User);
                SortContacts();
                chat.RaiseHeader();
                break;
            }
            case MessengerEvent.UserOffline u:
            {
                _online.Remove(u.UserId);
                if (_chats.TryGetValue(u.UserId, out var chat))
                {
                    chat.Online = false;
                    chat.Contact.Typing = false;
                    Contacts.Remove(chat.Contact);
                    chat.RaiseHeader();
                    if (_selected == chat) Raise(nameof(Selected));
                }
                break;
            }
            case MessengerEvent.AvatarUpdated a:
                if (_contactById.TryGetValue(a.UserId, out var ac) && a.Path != null) ac.Avatar = LoadBitmap(a.Path);
                break;
            case MessengerEvent.TextMessage t:
            {
                var chat = ChatFor(t.UserId);
                chat.Contact.Typing = false;
                chat.AddItem(new MessageVm(t.Text, false, chat.Title, t.TimeMs, t.MessageId, Delivery.Delivered, false));
                SaveHistory(chat.Key, chat.Title, chat.Title, t.Text, false, t.TimeMs);
                Incoming(chat, chat.Title, t.Text);
                ItemAdded?.Invoke(chat);
                break;
            }
            case MessengerEvent.MessageDelivered d:
                if (_chats.TryGetValue(d.UserId, out var dc)) foreach (var m in dc.Items.OfType<MessageVm>().Where(m => m.MessageId == d.MessageId)) m.State = Delivery.Delivered;
                break;
            case MessengerEvent.MessageFailed f:
                if (_chats.TryGetValue(f.UserId, out var fc)) foreach (var m in fc.Items.OfType<MessageVm>().Where(m => m.MessageId == f.MessageId)) m.State = Delivery.Failed;
                break;
            case MessengerEvent.BroadcastMessage b:
            {
                var chat = ChatFor(b.UserId);
                chat.AddItem(new BroadcastVm($"Broadcast from {chat.Title}", b.Text, b.TimeMs));
                SaveHistory(chat.Key, chat.Title, chat.Title, "[broadcast] " + b.Text, false, b.TimeMs);
                Incoming(chat, $"{chat.Title} · broadcast", b.Text);
                ItemAdded?.Invoke(chat);
                break;
            }
            case MessengerEvent.ChatStateChanged cs:
                if (cs.ThreadId.Length == 0 && _contactById.TryGetValue(cs.UserId, out var tc))
                {
                    tc.Typing = cs.State == ChatStateKind.Composing;
                    if (tc.Typing) DispatcherTimer.RunOnce(() => tc.Typing = false, TimeSpan.FromSeconds(10));
                }
                break;
            case MessengerEvent.RoomChanged rc:
            {
                var room = Rooms.FirstOrDefault(r => r.ThreadId == rc.Room.ThreadId);
                if (room == null)
                {
                    room = new RoomVm(this, rc.Room.ThreadId);
                    Rooms.Add(room);
                }
                room.Participants = rc.Room.Participants.ToList();
                room.RaiseHeader();
                RoomsChanged();
                break;
            }
            case MessengerEvent.RoomClosed rcl:
            {
                var room = Rooms.FirstOrDefault(r => r.ThreadId == rcl.ThreadId);
                if (room != null)
                {
                    if (_selected == room) Selected = null;
                    Rooms.Remove(room);
                }
                break;
            }
            case MessengerEvent.RoomNotice rn:
            {
                var room = Rooms.FirstOrDefault(r => r.ThreadId == rn.ThreadId);
                room?.AddItem(new NoticeVm($"{NameOf(rn.UserId)} {(rn.Joined ? "joined" : "left")}", rn.TimeMs));
                if (room != null) ItemAdded?.Invoke(room);
                break;
            }
            case MessengerEvent.RoomMessage rm:
            {
                var room = Rooms.FirstOrDefault(r => r.ThreadId == rm.ThreadId);
                if (room == null)
                {
                    room = new RoomVm(this, rm.ThreadId);
                    Rooms.Add(room);
                }
                var who = NameOf(rm.UserId);
                room.AddItem(new MessageVm(rm.Text, false, who, rm.TimeMs, 0, Delivery.Delivered, true));
                room.LastTime = rm.TimeMs;
                SaveHistory("group", "Group conversations", who, rm.Text, false, rm.TimeMs);
                Incoming(room, $"Group · {who}", rm.Text);
                ItemAdded?.Invoke(room);
                RoomsChanged();
                break;
            }
            case MessengerEvent.Transfer tr:
                OnTransfer(tr.Info);
                break;
            case MessengerEvent.UserInfo ui:
                InfoReceived?.Invoke(ui.UserId, ui.Info);
                break;
        }
    }

    public event Action<string, IReadOnlyDictionary<string, string>>? InfoReceived;
    public event Action? RoomListChanged;
    private void RoomsChanged() { RoomListChanged?.Invoke(); UnreadChanged?.Invoke(); Raise(nameof(TotalUnread)); }

    private string NameOf(string id) => _contactById.TryGetValue(id, out var c) ? c.Name : id;

    public string RoomTitle(RoomVm r)
    {
        var names = r.Participants.Where(p => p != LocalId).Select(NameOf).ToList();
        return names.Count == 0 ? "Group chat" : string.Join(", ", names);
    }

    private void Incoming(ConversationVm conv, string title, string text)
    {
        bool visible = WindowActive && _selected == conv;
        if (visible) return;
        if (conv is ChatVm c) c.Contact.Unread++;
        else if (conv is RoomVm r) r.Unread++;
        Raise(nameof(TotalUnread));
        UnreadChanged?.Invoke();
        if (Settings.Notifications)
        {
            Notification?.Invoke(title, text, conv.Key);
            if (!WindowActive) Shell.Notify(title, text);
        }
    }

    private void OnTransfer(TransferInfo t)
    {
        var chat = ChatFor(t.UserId);
        var vm = chat.Items.OfType<FileVm>().FirstOrDefault(f => f.FileId == t.Id);
        if (vm == null)
        {
            vm = new FileVm(t, OnFileAction);
            chat.AddItem(vm);
            if (!t.Outgoing && t.State == TransferState.Requested) Incoming(chat, chat.Title, $"Wants to send you {t.Name}");
            ItemAdded?.Invoke(chat);
        }
        else vm.Update(t);
    }

    private void OnFileAction(FileVm f, string action)
    {
        var userId = _chats.Values.FirstOrDefault(c => c.Items.Contains(f))?.Key;
        if (userId == null || Messenger == null) return;
        switch (action)
        {
            case "accept":
                Messenger.AcceptFile(userId, f.FileId, new LocalFileSink(Shell.FreeFileName(Settings.EffectiveDownloadDir, f.Name)));
                break;
            case "decline": Messenger.DeclineFile(userId, f.FileId); break;
            case "cancel": Messenger.CancelFile(userId, f.FileId); break;
            case "open": if (f.Location != null) Shell.Open(f.Location); break;
            case "show": if (f.Location != null) Shell.Reveal(f.Location); break;
        }
    }

    // ---- demo data (used for screenshots and UI tests, never in the real app) ---------------------------

    public void LoadDemo()
    {
        long t = Now() - 3_600_000;
        var users = new[]
        {
            new User("A1B2C3D4E5F6anna", "2.0.12", "10.0.0.21", "Anna Kovács", "chat", "Working from the lab", 7),
            new User("A1B2C3D4E5F7bela", "3.0.0", "10.0.0.22", "Béla Nagy", "busy", "In a meeting until 15:00", 7),
            new User("A1B2C3D4E5F8csilla", "3.0.0", "10.0.0.23", "Csilla Tóth", "away", "", 7),
            new User("A1B2C3D4E5F9david", "1.2.30", "10.0.0.24", "Dávid Szabó", "chat", "", 7),
        };
        foreach (var u in users)
        {
            var chat = ChatFor(u.Id, u);
            chat.Online = true;
            _online.Add(u.Id);
            if (!Contacts.Contains(chat.Contact)) Contacts.Add(chat.Contact);
        }
        SortContacts();
        Connected = true;
        Ip = "10.0.0.5";
        ConnectionText = "Online · 10.0.0.5";
        var anna = ChatFor(users[0].Id);
        anna.AddItem(new MessageVm("Hi! Did you get the report I sent yesterday?", false, "Anna Kovács", t, 1, Delivery.Delivered, false));
        anna.AddItem(new MessageVm("Yes, thanks - reading it now. The charts look great.", true, Name, t + 120_000, 2, Delivery.Delivered, false));
        anna.AddItem(new MessageVm("Can you send me the raw data too? https://example.org/data", true, Name, t + 180_000, 3, Delivery.Delivered, false));
        anna.AddItem(new FileVm(new TransferInfo("F0000000000000000000000000000001", anna.Key, "measurements-2026.xlsx", 2_457_600, 0, false, TransferState.Requested), OnFileAction));
        anna.AddItem(new FileVm(new TransferInfo("F0000000000000000000000000000002", anna.Key, "summary.pdf", 1_048_576, 524_288, true, TransferState.Transferring), OnFileAction));
        anna.AddItem(new MessageVm("Sending the summary now.", true, Name, t + 600_000, 4, Delivery.Sending, false));
        anna.AddItem(new BroadcastVm("Broadcast from Béla Nagy", "Coffee in the kitchen in 5 minutes!", t + 900_000));
        anna.AddItem(new MessageVm("On my way, thanks for the heads-up", true, Name, t + 960_000, 5, Delivery.Failed, false));
        ContactFor(users[0].Id).Unread = 0;
        ContactFor(users[1].Id).Unread = 3;
        ContactFor(users[2].Id).Typing = true;
        var room = new RoomVm(this, "ROOM0000000000000000000000000001") { Participants = new List<string> { "LOCAL", users[0].Id, users[1].Id } };
        Rooms.Add(room);
        room.AddItem(new NoticeVm("Béla Nagy joined", t));
        room.AddItem(new MessageVm("Lunch at 12:30?", false, "Béla Nagy", t + 60_000, 0, Delivery.Delivered, true));
        room.AddItem(new MessageVm("Works for me.", true, Name, t + 120_000, 0, Delivery.Delivered, false));
        Selected = anna;
    }

    // ---- actions -------------------------------------------------------------------------------------

    public void SelectChat(ContactVm c) => Selected = ChatFor(c.Id);
    public void SelectRoom(RoomVm r) => Selected = r;

    public void SendText(ChatVm chat, string text)
    {
        if (Messenger == null) return;
        long id = Messenger.SendText(chat.Key, text);
        long now = Now();
        chat.AddItem(new MessageVm(text, true, Name, now, id, Delivery.Sending, false));
        SaveHistory(chat.Key, chat.Title, Name, text, true, now);
        ItemAdded?.Invoke(chat);
    }

    public void SendRoomText(RoomVm room, string text)
    {
        if (Messenger == null) return;
        Messenger.SendGroupText(room.ThreadId, text);
        long now = Now();
        room.AddItem(new MessageVm(text, true, Name, now, 0, Delivery.Delivered, false));
        room.LastTime = now;
        SaveHistory("group", "Group conversations", Name, text, true, now);
        ItemAdded?.Invoke(room);
        RoomsChanged();
    }

    public void SendFile(ChatVm chat, string path)
    {
        if (Messenger == null || !chat.Online) return;
        Messenger.SendFile(chat.Key, new LocalFileSource(path));
    }

    public void Broadcast(string text)
    {
        if (Messenger == null) return;
        var ids = Contacts.Where(c => !Proto.IsOffline(c.Status)).Select(c => c.Id).ToList();
        Messenger.SendBroadcastText(ids, text);
        long now = Now();
        foreach (var id in ids)
        {
            var chat = _chats[id];
            chat.AddItem(new BroadcastVm("You sent a broadcast", text, now));
            ItemAdded?.Invoke(chat);
        }
    }

    public RoomVm? CreateRoom(IEnumerable<string> ids)
    {
        if (Messenger == null) return null;
        var thread = Messenger.CreateGroupChat(ids);
        var room = Rooms.FirstOrDefault(r => r.ThreadId == thread);
        if (room == null)
        {
            room = new RoomVm(this, thread);
            Rooms.Add(room);
        }
        RoomsChanged();
        return room;
    }

    public void InviteToRoom(RoomVm room, IEnumerable<string> ids) => Messenger?.InviteToRoom(room.ThreadId, ids);

    public void LeaveRoom(RoomVm room)
    {
        Messenger?.LeaveGroupChat(room.ThreadId);
        if (_selected == room) Selected = null;
        Rooms.Remove(room);
        RoomsChanged();
    }

    public void SetStatus(string code)
    {
        Settings.Status = code;
        Status = code;
        Messenger?.SetStatus(code);
        Settings.Save();
    }

    public void SetProfile(string name, string note)
    {
        name = name.Trim();
        if (name.Length > 0 && name != Name)
        {
            Settings.Name = name;
            Name = name;
            Messenger?.SetName(name);
        }
        if (note != Note)
        {
            Settings.Note = note;
            Note = note;
            Messenger?.SetNote(note);
        }
        Settings.Save();
    }

    public void SetAvatar(string? pngPath)
    {
        if (pngPath == null)
        {
            try { File.Delete(AvatarFile); } catch (IOException) { }
            MyAvatar = null;
            Messenger?.SetLocalAvatar(null);
            return;
        }
        MyAvatar = LoadBitmap(pngPath);
        Messenger?.SetLocalAvatar(pngPath);
    }

    public void Refresh() => Messenger?.Refresh();

    public void SaveHistory(string key, string title, string sender, string text, bool outgoing, long time)
    {
        if (Settings.SaveHistory) History.Append(key, title, new HistoryStore.Entry(time, outgoing, sender, text));
    }

    public static Bitmap? LoadBitmap(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            return Bitmap.DecodeToWidth(fs, 160);
        }
        catch (Exception) { return null; }
    }
}
