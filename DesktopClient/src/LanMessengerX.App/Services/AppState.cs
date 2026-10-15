using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LanMessengerX.App.Localization;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Services;

/// <summary>Glue between the protocol engine and the view models. Lives on the UI thread.</summary>
public sealed class AppState : Observable
{
    public static AppState Current { get; private set; } = null!;

    public AppSettings Settings { get; }
    public HistoryStore History { get; private set; }
    public Messenger? Messenger { get; private set; }
    public ObservableCollection<ContactVm> Contacts { get; } = new();
    /// <summary>What the people list shows: group headers and the contacts below them.</summary>
    public ObservableCollection<RosterRow> Roster { get; } = new();
    public ObservableCollection<RoomVm> Rooms { get; } = new();
    public PublicVm PublicChat { get; }
    /// <summary>Every file transfer of this session, newest first (the File Transfers window).</summary>
    public ObservableCollection<FileVm> Transfers { get; } = new();
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
        Loc.Use(settings.Language);
        History = new HistoryStore(settings.EffectiveHistoryDir);
        PublicChat = new PublicVm(this);
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
        if (settings.Groups.Count == 0 && settings.UserGroups.Count == 0) { /* no custom groups: flat list */ }
        Avatars.EnsureDefault(this);
        if (File.Exists(AvatarFile)) _myAvatar = LoadBitmap(AvatarFile);
    }

    public IReadOnlyList<Bitmap?> StockAvatars { get; } = Enumerable.Range(0, Avatars.StockCount).Select(Avatars.Stock).ToList();

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
            var previous = _selected;
            if (!Set(ref _selected, value)) return;
            if (Settings.ClearOnClose && previous != null && previous != value) previous.Items.Clear();
            if (value is ChatVm c) { c.Contact.Unread = 0; }
            if (value is RoomVm r) { r.Unread = 0; RoomsChanged(); }
            if (value is PublicVm pv) { pv.Unread = 0; RoomsChanged(); }
            Raise(nameof(HasSelection));
            Raise(nameof(TotalUnread));
        }
    }

    public bool HasSelection => _selected != null;
    public int TotalUnread => Contacts.Sum(c => c.Unread) + Rooms.Sum(r => r.Unread) + PublicChat.Unread;
    public event Action? UnreadChanged;

    // ---- lifecycle ----------------------------------------------------------------------------------

    public MessengerConfig BuildConfig() => new()
    {
        UserName = Settings.Name,
        Status = Settings.Status,
        Note = Settings.Note,
        MachineId = Settings.MachineId,
        LogonName = Environment.UserName,
        HostName = Environment.MachineName,
        OsName = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        CacheDir = CacheDir,
        UdpPort = Settings.UdpPort,
        TcpPort = Settings.TcpPort,
        Multicast = string.IsNullOrWhiteSpace(Settings.Multicast) ? Proto.DefaultMulticast : Settings.Multicast,
        TimeoutSec = Math.Max(1, Settings.TimeoutSeconds),
        MaxRetries = Math.Max(0, Settings.MaxRetries),
        RefreshSec = Settings.RefreshSeconds,
        PreferredInterface = Settings.PreferredInterface,
        ScanSubnet = Settings.ScanSubnet,
        ExtraBroadcasts = Settings.ExtraBroadcasts.ToList(),
        FirstName = Settings.FirstName,
        LastName = Settings.LastName,
        About = Settings.About,
        MessageFont = FontSpec(Settings.MessageFont, Settings.MessageFontSize),
        MessageColor = Settings.MessageColor,
    };

    /// <summary>Qt's QFont string form, which the original clients parse.</summary>
    public static string FontSpec(string family, int size) => $"{family},{size},-1,5,50,0,0,0,0,0";

    public void Start()
    {
        FileLog.Reset();
        Messenger = new Messenger(BuildConfig(), FileLog.Write, e => Dispatcher.UIThread.Post(() => OnEvent(e)));
        Messenger.Start();
        if (File.Exists(AvatarFile)) Messenger.SetLocalAvatar(AvatarFile);
        RebuildRoster();
    }

    /// <summary>Called after the settings window saved: everything that can change without a restart.</summary>
    public void ApplySettings()
    {
        Loc.Use(Settings.Language);
        Messenger?.UpdateDetails(Settings.FirstName, Settings.LastName, Settings.About, FontSpec(Settings.MessageFont, Settings.MessageFontSize), Settings.MessageColor);
        if (Messenger != null) Messenger.PreferredInterface = Settings.PreferredInterface;
        Autostart.Set(Settings.AutoStart);
        (Avalonia.Application.Current as App)?.ApplyTheme(Settings.Theme);
        (Avalonia.Application.Current as App)?.ApplyTray();
        RebuildRoster();
        Raise(nameof(ConnectionText));
        Settings.Save();
        SettingsApplied?.Invoke();
    }

    public event Action? SettingsApplied;

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
        RebuildRoster();
    }

    // ---- contact groups ----------------------------------------------------------------------------

    public IReadOnlyList<AppSettings.ContactGroup> Groups => Settings.Groups;

    /// <summary>Group headers (only once the user made a group of their own) and the contacts below them, collapsed groups without contacts.</summary>
    public void RebuildRoster()
    {
        var keepSelected = _selected;
        Roster.Clear();
        var known = Settings.Groups.Select(g => g.Id).ToHashSet();
        var ordered = Contacts.OrderBy(c => Proto.IsOffline(c.Status)).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var c in ordered) if (!known.Contains(c.GroupId)) c.GroupId = "";
        if (Settings.Groups.Count == 0)
        {
            foreach (var c in ordered) Roster.Add(c);
            return;
        }
        foreach (var g in Settings.Groups.Prepend(new AppSettings.ContactGroup { Id = "", Name = Loc.T("General"), Collapsed = _generalCollapsed }))
        {
            var members = ordered.Where(c => c.GroupId == g.Id).ToList();
            Roster.Add(new GroupRowVm(g.Id, g.Name, g.Collapsed, members.Count));
            if (!g.Collapsed) foreach (var c in members) Roster.Add(c);
        }
        _selected = keepSelected;
        RosterChanged?.Invoke();
    }

    private bool _generalCollapsed;
    public event Action? RosterChanged;

    public void ToggleGroup(GroupRowVm row)
    {
        if (row.IsDefault) _generalCollapsed = !_generalCollapsed;
        else if (Settings.Groups.FirstOrDefault(g => g.Id == row.Id) is { } g) g.Collapsed = !g.Collapsed;
        Settings.Save();
        RebuildRoster();
    }

    public void AddGroup(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || Settings.Groups.Any(g => string.Equals(g.Name, name, StringComparison.CurrentCultureIgnoreCase))) return;
        Settings.Groups.Add(new AppSettings.ContactGroup { Name = name });
        Settings.Save();
        RebuildRoster();
    }

    public bool GroupExists(string name) => Settings.Groups.Any(g => string.Equals(g.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));

    public void RenameGroup(string id, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || Settings.Groups.FirstOrDefault(g => g.Id == id) is not { } g) return;
        g.Name = name;
        Settings.Save();
        RebuildRoster();
    }

    public void DeleteGroup(string id)
    {
        Settings.Groups.RemoveAll(g => g.Id == id);
        foreach (var k in Settings.UserGroups.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList()) Settings.UserGroups.Remove(k);
        foreach (var c in Contacts.Where(c => c.GroupId == id)) c.GroupId = "";
        Settings.Save();
        RebuildRoster();
    }

    public void MoveContact(ContactVm c, string groupId)
    {
        c.GroupId = groupId;
        if (groupId.Length == 0) Settings.UserGroups.Remove(c.Id);
        else Settings.UserGroups[c.Id] = groupId;
        Settings.Save();
        RebuildRoster();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void OnEvent(MessengerEvent e)
    {
        switch (e)
        {
            case MessengerEvent.ConnectionChanged c:
                Connected = c.Connected;
                Ip = c.Ip;
                ConnectionText = c.Connected ? Loc.F("Online · {0}", c.Ip ?? "") : Loc.T("Waiting for a network…");
                break;
            case MessengerEvent.UserOnline u:
            {
                var chat = ChatFor(u.User.Id, u.User);
                bool wasOnline = chat.Online;
                chat.Online = true;
                _online.Add(u.User.Id);
                chat.Contact.GroupId = Settings.UserGroups.GetValueOrDefault(u.User.Id, "");
                if (!Contacts.Contains(chat.Contact)) Contacts.Add(chat.Contact);
                SortContacts();
                chat.RaiseHeader();
                if (!wasOnline && !Proto.IsOffline(u.User.Status)) StatusAlert(Loc.F("{0} is online.", u.User.Name), SoundEvent.UserOnline);
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
                    RebuildRoster();
                    chat.RaiseHeader();
                    if (_selected == chat) Raise(nameof(Selected));
                    StatusAlert(Loc.F("{0} is offline.", u.Name), SoundEvent.UserOffline);
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
                Incoming(chat, chat.Title, t.Text, SoundEvent.NewMessage);
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
                chat.AddItem(new BroadcastVm(Loc.F("Broadcast from {0}", chat.Title), b.Text, b.TimeMs));
                SaveHistory(chat.Key, chat.Title, chat.Title, "[broadcast] " + b.Text, false, b.TimeMs);
                Incoming(chat, Loc.F("{0} · broadcast", chat.Title), b.Text, SoundEvent.NewMessage);
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
                room?.AddItem(new NoticeVm(Loc.F(rn.Joined ? "{0} joined" : "{0} left", NameOf(rn.UserId)), rn.TimeMs));
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
                SaveHistory("group", Loc.T("Group conversations"), who, rm.Text, false, rm.TimeMs);
                Incoming(room, Loc.F("Group · {0}", who), rm.Text, SoundEvent.NewMessage);
                ItemAdded?.Invoke(room);
                RoomsChanged();
                break;
            }
            case MessengerEvent.PublicMessage pm:
            {
                var who = NameOf(pm.UserId);
                PublicChat.AddItem(new MessageVm(pm.Text, false, who, pm.TimeMs, 0, Delivery.Delivered, true));
                SaveHistory(PublicVm.KeyValue, Loc.T("Public chat"), who, pm.Text, false, pm.TimeMs);
                Incoming(PublicChat, Loc.F("Public chat · {0}", who), pm.Text, SoundEvent.NewMessage);
                ItemAdded?.Invoke(PublicChat);
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
        return names.Count == 0 ? Loc.T("Group chat") : string.Join(", ", names);
    }

    private void Incoming(ConversationVm conv, string title, string text, SoundEvent sound)
    {
        bool visible = WindowActive && _selected == conv;
        if (visible) return;
        if (conv is ChatVm c) c.Contact.Unread++;
        else if (conv is RoomVm r) r.Unread++;
        else if (conv is PublicVm p) p.Unread++;
        Raise(nameof(TotalUnread));
        UnreadChanged?.Invoke();
        Sounds.Play(Settings, Status, sound);
        if (Settings.Notifications && !SuspendedAlerts)
        {
            Notification?.Invoke(title, text, conv.Key);
            if (!WindowActive) Shell.Notify(title, text);
        }
    }

    private bool SuspendedAlerts => (Settings.NoBusyAlert && Status == "busy") || (Settings.NoDndAlert && Status == "dnd");

    /// <summary>"X is online/offline" popup and sound (the original's status alerts).</summary>
    private void StatusAlert(string text, SoundEvent sound)
    {
        Sounds.Play(Settings, Status, sound);
        if (Settings.StatusAlerts && !SuspendedAlerts && Settings.Notifications) Shell.Notify("LAN Messenger X", text);
    }

    private void OnTransfer(TransferInfo t)
    {
        var chat = ChatFor(t.UserId);
        var vm = chat.Items.OfType<FileVm>().FirstOrDefault(f => f.FileId == t.Id);
        if (vm == null)
        {
            vm = new FileVm(t, OnFileAction) { Peer = chat.Title };
            chat.AddItem(vm);
            Transfers.Insert(0, vm);
            if (!t.Outgoing && t.State == TransferState.Requested)
            {
                Sounds.Play(Settings, Status, SoundEvent.NewFile);
                if (Settings.AutoAcceptFiles && Messenger != null)
                {
                    if (t.IsFolder) Messenger.AcceptFolder(t.UserId, t.Id, Settings.EffectiveDownloadDir);
                    else Messenger.AcceptFile(t.UserId, t.Id, new LocalFileSink(Shell.FreeFileName(Settings.EffectiveDownloadDir, vm.Name)));
                }
                else Incoming(chat, chat.Title, Loc.F("Wants to send you {0}", t.Name), SoundEvent.NewFile);
                if (Settings.ShowTransferWindow) TransferStarted?.Invoke();
            }
            else if (t.Outgoing && Settings.ShowTransferWindow) TransferStarted?.Invoke();
            ItemAdded?.Invoke(chat);
        }
        else
        {
            var before = vm.State;
            vm.Update(t);
            if (before != vm.State && vm.State == TransferState.Complete)
            {
                Sounds.Play(Settings, Status, SoundEvent.FileDone);
                LogTransfer(vm);
            }
            else if (before != vm.State && vm.State is TransferState.Declined or TransferState.Cancelled or TransferState.Error) LogTransfer(vm);
        }
    }

    public event Action? TransferStarted;

    private void LogTransfer(FileVm f)
    {
        if (!Settings.SaveFileHistory) return;
        TransferLog.Append(new TransferLog.Entry(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), f.Peer, f.Name, f.Size, f.Outgoing, f.State.ToString(), f.Location));
    }

    private void OnFileAction(FileVm f, string action)
    {
        var userId = _chats.Values.FirstOrDefault(c => c.Items.Contains(f))?.Key;
        if (userId == null) userId = _chats.Values.FirstOrDefault(c => c.Title == f.Peer)?.Key;
        if (userId == null || Messenger == null) return;
        switch (action)
        {
            case "accept":
                if (f.IsFolder) Messenger.AcceptFolder(userId, f.FileId, Settings.EffectiveDownloadDir);
                else Messenger.AcceptFile(userId, f.FileId, new LocalFileSink(Shell.FreeFileName(Settings.EffectiveDownloadDir, f.Name)));
                break;
            case "decline": if (f.IsFolder) Messenger.DeclineFolder(userId, f.FileId); else Messenger.DeclineFile(userId, f.FileId); break;
            case "cancel": if (f.IsFolder) Messenger.CancelFolder(userId, f.FileId); else Messenger.CancelFile(userId, f.FileId); break;
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

    public void SendPublicText(PublicVm pub, string text)
    {
        if (Messenger == null) return;
        Messenger.SendPublicText(text);
        long now = Now();
        pub.AddItem(new MessageVm(text, true, Name, now, 0, Delivery.Delivered, false));
        SaveHistory(PublicVm.KeyValue, Loc.T("Public chat"), Name, text, true, now);
        ItemAdded?.Invoke(pub);
    }

    public ChatVm ChatOf(ContactVm c) => ChatFor(c.Id);

    public void SendFolder(ChatVm chat, string path)
    {
        if (Messenger == null || !chat.Online) return;
        Messenger.SendFolder(chat.Key, path);
    }

    /// <summary>The history folder was changed in the settings.</summary>
    public void UseHistoryFolder() => History = new HistoryStore(Settings.EffectiveHistoryDir);

    /// <summary>Preferences back to their defaults; who you are (name, picture, groups, id) stays.</summary>
    public void ResetPreferences()
    {
        var fresh = new AppSettings();
        var keep = new HashSet<string> { nameof(AppSettings.MachineId), nameof(AppSettings.Name), nameof(AppSettings.Note), nameof(AppSettings.Status),
            nameof(AppSettings.StockAvatar), nameof(AppSettings.Groups), nameof(AppSettings.UserGroups), nameof(AppSettings.WindowWidth), nameof(AppSettings.WindowHeight) };
        foreach (var p in typeof(AppSettings).GetProperties())
            if (p.CanWrite && !keep.Contains(p.Name)) p.SetValue(Settings, p.GetValue(fresh));
        UseHistoryFolder();
        ApplySettings();
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
        Broadcast(text, ids);
    }

    /// <summary>A broadcast message to chosen people (they all get the same text).</summary>
    public void Broadcast(string text, IReadOnlyCollection<string> ids)
    {
        if (Messenger == null || ids.Count == 0) return;
        Messenger.SendBroadcastText(ids, text);
        long now = Now();
        foreach (var id in ids)
        {
            if (!_chats.TryGetValue(id, out var chat)) continue;
            chat.AddItem(new BroadcastVm(Loc.T("You sent a broadcast"), text, now));
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

    /// <summary>The line under the name ("status message"): shown to everyone next to the name.</summary>
    public void SetNote(string note)
    {
        note = note.Trim();
        if (note == Note) return;
        Settings.Note = note;
        Note = note;
        Messenger?.SetNote(note);
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
