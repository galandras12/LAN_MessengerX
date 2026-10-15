using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LanMessengerX.Core;

public sealed class MessengerConfig
{
    public string UserName { get; set; } = "";
    public string Status { get; set; } = Proto.StatusOnline;
    public string Note { get; set; } = "";
    /// <summary>12 hex digits identifying this device (the original uses the NIC's MAC address).</summary>
    public string MachineId { get; set; } = "";
    public string LogonName { get; set; } = "";
    public string HostName { get; set; } = "";
    public string OsName { get; set; } = "";
    public string CacheDir { get; set; } = "";
    public int UdpPort { get; set; } = Proto.DefaultPort;
    public int TcpPort { get; set; } = Proto.DefaultPort;
    public string Multicast { get; set; } = Proto.DefaultMulticast;
    public List<string> ExtraBroadcasts { get; set; } = new();
    public int TimeoutSec { get; set; } = 10;
    public int MaxRetries { get; set; } = 1;
    public string? PreferredInterface { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string About { get; set; } = "";
}

/// <summary>
/// The LAN Messenger protocol engine. All protocol state lives on one engine thread; public methods
/// may be called from any thread. Events are delivered on the engine thread - UI code has to hop to
/// its own thread.
/// </summary>
public sealed class Messenger : IDisposable
{
    public const string DefaultFont = "Segoe UI,9,-1,5,50,0,0,0,0,0";
    public const string DefaultColor = "#000000";
    private const int PacedDelayMs = 3;
    private const int FileChunk = 65535;

    private readonly MessengerConfig _cfg;
    private readonly Action<string> _log;
    private readonly Action<MessengerEvent> _listener;

    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _engineThread;
    private readonly List<Timer> _timers = new();
    private readonly CancellationTokenSource _cts = new();

    public string LocalId { get; }

    /// <summary>Takes effect on the next network check (every two seconds).</summary>
    public string? PreferredInterface { get => _cfg.PreferredInterface; set => _cfg.PreferredInterface = value; }

    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, Conn> _streams = new();
    private readonly Dictionary<string, SessionCipher> _keys = new();
    private readonly HashSet<string> _connecting = new();
    private readonly Dictionary<string, List<string>> _rooms = new();
    private readonly List<Pending> _pending = new();
    private readonly LinkedList<string> _receivedOrder = new();
    private readonly HashSet<string> _received = new();
    private readonly ConcurrentDictionary<string, Transfer> _transfers = new();
    private long _msgIdSeq = 1;

    private LmxCrypto.RsaKeys? _rsa;
    private LanInterface? _lan;
    private UdpService? _udp;
    private TcpServer? _tcp;
    private bool _started;
    private bool _stopped;
    private string? _localAvatar;

    private volatile IReadOnlyList<User> _snapshot = Array.Empty<User>();
    private User _localUser;
    private readonly object _roomsLock = new();
    private Dictionary<string, IReadOnlyList<string>> _roomsSnapshot = new();

    public bool IsConnected { get; private set; }
    public IReadOnlyList<User> OnlineUsers => _snapshot;
    public User LocalUser => Volatile.Read(ref _localUser);

    private sealed class Pending
    {
        public long MsgId;
        public MessageType Type;
        public string UserId = "";
        public XmlMessage Body = new();
        public long SentAt;
        public int Retry;
    }

    private sealed class Transfer
    {
        public string Id = "";
        public string UserId = "";
        public string Name = "";
        public long Size;
        public bool Outgoing;
        public FileType Type;
        public volatile TransferState State;
        public IFileSource? Source;
        public volatile IFileSink? Sink;
        public long Bytes;
        public volatile bool Cancelled;
        public volatile Socket? Socket;
    }

    public Messenger(MessengerConfig cfg, Action<string>? log, Action<MessengerEvent> listener)
    {
        _cfg = cfg;
        _log = log ?? (_ => { });
        _listener = listener;
        LocalId = BuildUserId(cfg.MachineId, cfg.LogonName);
        _localUser = new User(LocalId, Proto.Version, "", cfg.UserName, cfg.Status, cfg.Note, Proto.UcFile | Proto.UcGroupMessage);
        _engineThread = new Thread(EngineLoop) { IsBackground = true, Name = "lmx-engine" };
        _engineThread.Start();
    }

    // ==========================================================================================
    // engine thread plumbing
    // ==========================================================================================

    private void EngineLoop()
    {
        foreach (var a in _queue.GetConsumingEnumerable())
        {
            try { a(); }
            catch (Exception e) { _log($"engine error: {e}"); }
        }
    }

    private void Post(Action a)
    {
        try { if (!_queue.IsAddingCompleted) _queue.Add(a); }
        catch (InvalidOperationException) { }
    }

    private void Emit(MessengerEvent e)
    {
        try { _listener(e); }
        catch (Exception t) { _log($"listener error: {t}"); }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long NextId() => Interlocked.Increment(ref _msgIdSeq) - 1;

    // ==========================================================================================
    // lifecycle
    // ==========================================================================================

    public void Start()
    {
        Post(() =>
        {
            if (_started) return;
            _started = true;
            Task.Run(() =>
            {
                var keys = LmxCrypto.GenerateRsa();
                Post(() => { _rsa = keys; NetworkTick(); });
            });
            _timers.Add(new Timer(_ => Post(NetworkTick), null, 2000, 2000));
            _timers.Add(new Timer(_ => Post(CheckPending), null, 1000, 1000));
            _timers.Add(new Timer(_ => Post(KeepAlive), null, 30000, 30000));
        });
    }

    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        var done = new ManualResetEventSlim();
        Post(() =>
        {
            if (_started)
            {
                if (IsConnected) Broadcast(MessageType.Depart);
                TeardownNetwork();
                _started = false;
            }
            foreach (var t in _timers) t.Dispose();
            done.Set();
        });
        done.Wait(TimeSpan.FromSeconds(3));
        _queue.CompleteAdding();
        _cts.Cancel();
    }

    public void Dispose() => Stop();

    // ==========================================================================================
    // network up/down
    // ==========================================================================================

    private void NetworkTick()
    {
        if (!_started || _rsa == null) return;
        var found = NetUtil.FindLanInterface(_cfg.PreferredInterface);
        if (found == null)
        {
            if (IsConnected)
            {
                _log("network lost");
                TeardownNetwork();
                Emit(new MessengerEvent.ConnectionChanged(false, null));
            }
            return;
        }
        if (IsConnected && found.SameAs(_lan)) return;
        if (IsConnected) TeardownNetwork();
        _lan = found;
        _log($"network up: {found.Name} ({found.Description}) {found.Ip}/{found.PrefixLength}, broadcast {found.Broadcast}");
        foreach (var c in NetUtil.ListInterfaces()) _log($"  adapter: {c.Name} ({c.Description}) {c.Ip}/{c.PrefixLength}{(c.SameAs(found) ? "  <- used" : "")}");
        var server = new TcpServer(_cfg.TcpPort, s => Task.Run(() => HandleIncoming(s)), _log);
        _tcp = server.Start() ? server : null;
        var u = new UdpService(found, _cfg.UdpPort, IPAddress.Parse(_cfg.Multicast), (ip, text) => Post(() => OnBroadcastDatagram(ip, text)), _log);
        u.Start();
        _udp = u;
        IsConnected = true;
        Volatile.Write(ref _localUser, _localUser with { Address = found.Ip });
        Emit(new MessengerEvent.ConnectionChanged(true, found.Ip));
        Broadcast(MessageType.Depart);
        Broadcast(MessageType.Announce);
    }

    private void TeardownNetwork()
    {
        _udp?.Stop(); _udp = null;
        _tcp?.Stop(); _tcp = null;
        foreach (var c in _streams.Values.ToList()) c.Close();
        _streams.Clear();
        _keys.Clear();
        _connecting.Clear();
        var gone = _users.Values.ToList();
        _users.Clear();
        _pending.Clear();
        RefreshSnapshot();
        foreach (var u in gone) Emit(new MessengerEvent.UserOffline(u.Id, u.Name));
        foreach (var t in _transfers.Values) CancelTransferLocally(t, TransferState.Error);
        IsConnected = false;
        _lan = null;
    }

    private void KeepAlive()
    {
        if (!IsConnected) return;
        Broadcast(MessageType.Announce);
        foreach (var u in _users.Values.ToList())
        {
            long id = NextId();
            var body = new XmlMessage();
            _pending.Add(new Pending { MsgId = id, Type = MessageType.Ping, UserId = u.Id, Body = body, SentAt = Now() });
            Transmit(u.Id, MessageType.Ping, id, body);
        }
    }

    // ==========================================================================================
    // UDP broadcast (announce / depart)
    // ==========================================================================================

    private void Broadcast(MessageType type)
    {
        var lan = _lan;
        var udp = _udp;
        if (lan == null || udp == null || LocalId.Length == 0) return;
        var m = new XmlMessage();
        m.AddHeader(Xn.From, LocalId);
        m.AddHeader(Xn.MessageId, Interlocked.Read(ref _msgIdSeq).ToString());
        m.AddHeader(Xn.Type, type.Name());
        var targets = new List<IPAddress>();
        void Add(IPAddress a) { if (!targets.Contains(a)) targets.Add(a); }
        try
        {
            Add(IPAddress.Parse(_cfg.Multicast));
            Add(lan.Broadcast);
            Add(IPAddress.Broadcast);
            foreach (var b in _cfg.ExtraBroadcasts) if (IPAddress.TryParse(b, out var ip)) Add(ip);
        }
        catch (Exception e) { _log($"broadcast target error: {e.Message}"); }
        if (type == MessageType.Announce && !_loggedTargets)
        {
            _loggedTargets = true;
            _log($"announcing {LocalId} to {string.Join(", ", targets)}");
        }
        udp.Send(Encoding.UTF8.GetBytes(m.ToXml()), targets);
    }

    private bool _loggedTargets;
    private readonly HashSet<string> _seenSenders = new();

    private void OnBroadcastDatagram(string fromIp, string text)
    {
        var m = XmlMessage.Parse(text);
        if (m == null) { _log($"UDP from {fromIp}: not a LAN Messenger datagram ({text.Length} chars)"); return; }
        var from = m.Header(Xn.From);
        if (from.Length == 0 || from == LocalId) return;
        if (!_seenSenders.Contains(from)) { _seenSenders.Add(from); _log($"UDP {m.Header(Xn.Type)} from {from} @ {fromIp}"); }
        switch (Wire.ParseMessageType(m.Header(Xn.Type)))
        {
            case MessageType.Announce:
                if (!_users.ContainsKey(from) && !_streams.ContainsKey(from) && _connecting.Add(from)) ConnectTo(from, fromIp);
                break;
            case MessageType.Depart:
                RemoveUser(from);
                break;
        }
    }

    // ==========================================================================================
    // TCP: connection establishment
    // ==========================================================================================

    private void ConnectTo(string userId, string ip)
    {
        Task.Run(() =>
        {
            Conn? conn = null;
            try
            {
                _log($"TCP connect to {userId} @ {ip}:{_cfg.TcpPort}");
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var connect = s.ConnectAsync(new IPEndPoint(IPAddress.Parse(ip), _cfg.TcpPort));
                if (!connect.Wait(5000)) { s.Close(); throw new TimeoutException("connect timed out"); }
                conn = new Conn(s, incoming: false, peerId: userId);
                _log($"TCP connected to {userId}, waiting for its public key");
                conn.SendRaw(Encoding.UTF8.GetBytes("MSG" + LocalId));
                ReadLoop(conn);
            }
            catch (Exception e)
            {
                _log($"connect to {userId}@{ip} failed: {e.Message}");
                conn?.Close();
                Post(() => _connecting.Remove(userId));
            }
        });
    }

    private void HandleIncoming(Socket s)
    {
        try
        {
            s.ReceiveTimeout = 5000;
            var first = new byte[Proto.IdPacketMax];
            int n = s.Receive(first);
            if (n <= 0) { s.Close(); return; }
            string head = Encoding.ASCII.GetString(first, 0, Math.Min(n, 4));
            if (head.StartsWith("MSG", StringComparison.Ordinal))
            {
                s.ReceiveTimeout = 0;
                var peerId = Encoding.UTF8.GetString(first, 3, n - 3);
                _log($"TCP incoming from {peerId} @ {(s.RemoteEndPoint as IPEndPoint)?.Address}");
                var conn = new Conn(s, incoming: true, peerId: peerId);
                var key = _rsa?.PublicPem;
                if (key == null) { conn.Close(); return; }
                conn.SendFrame(DatagramType.PublicKey, key);
                ReadLoop(conn);
            }
            else if (head == "FILE")
            {
                while (n < 4 + Proto.FileIdLength + 1)
                {
                    int more = s.Receive(first, n, first.Length - n, SocketFlags.None);
                    if (more <= 0) break;
                    n += more;
                }
                if (n < 4 + Proto.FileIdLength) { s.Close(); return; }
                var fileId = Encoding.ASCII.GetString(first, 4, Proto.FileIdLength);
                var userPrefix = Encoding.UTF8.GetString(first, 4 + Proto.FileIdLength, n - 4 - Proto.FileIdLength);
                if (!_transfers.TryGetValue(fileId, out var t) || t.Outgoing ||
                    !(t.UserId.StartsWith(userPrefix, StringComparison.Ordinal) || userPrefix.StartsWith(t.UserId, StringComparison.Ordinal)))
                {
                    s.Close(); return;
                }
                s.ReceiveTimeout = 30000;
                ReceiveFileLoop(t, s);
            }
            else s.Close();
        }
        catch (Exception e)
        {
            _log($"incoming connection error: {e.Message}");
            try { s.Close(); } catch (Exception) { }
        }
    }

    /// <summary>Runs on the connection's reader thread.</summary>
    private void ReadLoop(Conn conn)
    {
        try
        {
            while (true)
            {
                var frame = conn.ReadFrame();
                if (frame == null) break;
                Post(() => OnFrame(conn, frame));
            }
        }
        finally
        {
            conn.Close();
            Post(() => OnConnClosed(conn));
        }
    }

    private void RegisterStream(Conn conn)
    {
        if (_streams.TryGetValue(conn.PeerId, out var old) && !ReferenceEquals(old, conn)) _log($"replacing stream of {conn.PeerId}");
        _streams[conn.PeerId] = conn;
        if (conn.Cipher != null) _keys[conn.PeerId] = conn.Cipher;
        _connecting.Remove(conn.PeerId);
    }

    private void OnConnClosed(Conn conn)
    {
        _connecting.Remove(conn.PeerId);
        if (_streams.TryGetValue(conn.PeerId, out var cur) && ReferenceEquals(cur, conn))
        {
            _streams.Remove(conn.PeerId);
            _keys.Remove(conn.PeerId);
            RemoveUser(conn.PeerId);
        }
    }

    private void OnFrame(Conn conn, byte[] frame)
    {
        if (frame.Length < 6) return;
        var tag = Encoding.ASCII.GetString(frame, 0, 6);
        var data = frame.AsSpan(6).ToArray();
        switch (DatagramTags.FromTag(tag))
        {
            case DatagramType.PublicKey:
                try
                {
                    using var pub = LmxCrypto.PublicKeyFromPem(data);
                    var material = LmxCrypto.NewSessionMaterial();
                    conn.Cipher = new SessionCipher(material);
                    conn.SendFrame(DatagramType.Handshake, LmxCrypto.RsaEncrypt(pub, material));
                    _log($"key exchange with {conn.PeerId}: {pub.KeySize} bit public key, session key sent");
                    RegisterStream(conn);
                }
                catch (Exception e)
                {
                    _log($"handshake with {conn.PeerId} failed: {e.Message}");
                    conn.Close();
                }
                break;
            case DatagramType.Handshake:
                try
                {
                    var material = LmxCrypto.RsaDecrypt(_rsa!.Rsa, data);
                    conn.Cipher = new SessionCipher(material);
                    _log($"key exchange with {conn.PeerId}: session key received");
                    RegisterStream(conn);
                    SendUserData(conn.PeerId, QueryOp.Get);
                }
                catch (Exception e)
                {
                    _log($"handshake from {conn.PeerId} failed: {e.Message}");
                    conn.Close();
                }
                break;
            case DatagramType.Message:
                var clear = conn.Cipher?.Decrypt(data) ?? (_keys.TryGetValue(conn.PeerId, out var k) ? k.Decrypt(data) : null);
                if (clear == null)
                {
                    _log($"could not decrypt message from {conn.PeerId}");
                    return;
                }
                var m = XmlMessage.Parse(Encoding.UTF8.GetString(clear));
                if (m != null) ProcessMessage(conn, m);
                break;
        }
    }

    // ==========================================================================================
    // sending
    // ==========================================================================================

    private bool Transmit(string userId, MessageType type, long id, XmlMessage body)
    {
        if (!_streams.TryGetValue(userId, out var conn) || conn.Cipher == null) return false;
        var m = new XmlMessage();
        m.AddHeader(Xn.From, LocalId);
        m.AddHeader(Xn.To, userId);
        m.AddHeader(Xn.MessageId, id.ToString());
        m.AddHeader(Xn.Type, type.Name());
        m.Body.AddRange(body.Body);
        try
        {
            conn.SendFrame(DatagramType.Message, conn.Cipher.Encrypt(Encoding.UTF8.GetBytes(m.ToXml())));
            return true;
        }
        catch (Exception e)
        {
            _log($"send failed: {e.Message}");
            return false;
        }
    }

    private void SendUserData(string userId, QueryOp op)
    {
        var u = LocalUser;
        var b = new XmlMessage();
        b.AddData(Xn.UserId, LocalId);
        b.AddData(Xn.Name, u.Name);
        b.AddData(Xn.Address, u.Address);
        b.AddData(Xn.Version, u.Version);
        b.AddData(Xn.Status, u.Status);
        b.AddData(Xn.Note, u.Note);
        b.AddData(Xn.UserCaps, u.Caps.ToString());
        b.AddData(Xn.QueryOp, op.Name());
        Transmit(userId, MessageType.UserData, Interlocked.Read(ref _msgIdSeq), b);
    }

    private void Ack(string userId, long id) =>
        Transmit(userId, MessageType.Acknowledge, NextId(), new XmlMessage().AddData(Xn.MessageId, id.ToString()));

    private bool SendTracked(string userId, MessageType type, XmlMessage body, long id)
    {
        _pending.Add(new Pending { MsgId = id, Type = type, UserId = userId, Body = body, SentAt = Now() });
        return Transmit(userId, type, id, body);
    }

    private void CheckPending()
    {
        long t = Now();
        var failed = new List<Pending>();
        foreach (var p in _pending.ToList())
        {
            if (t - p.SentAt <= _cfg.TimeoutSec * 1000L) continue;
            if (p.Retry < _cfg.MaxRetries)
            {
                p.Retry++;
                p.SentAt = t;
                if (_users.ContainsKey(p.UserId)) Transmit(p.UserId, p.Type, p.MsgId, p.Body);
            }
            else
            {
                _pending.Remove(p);
                failed.Add(p);
            }
        }
        foreach (var p in failed)
        {
            if (p.Type == MessageType.Message) Emit(new MessengerEvent.MessageFailed(p.UserId, p.MsgId));
            else if (p.Type == MessageType.Ping) RemoveUser(p.UserId, close: true);
        }
    }

    // ---- public chat API --------------------------------------------------------------------

    /// <returns>The message id used to correlate MessageDelivered/MessageFailed events.</returns>
    public long SendText(string userId, string text)
    {
        long id = NextId();
        Post(() =>
        {
            if (!_users.ContainsKey(userId)) { Emit(new MessengerEvent.MessageFailed(userId, id)); return; }
            var b = new XmlMessage();
            b.AddData(Xn.Message, text);
            b.AddData(Xn.Font, DefaultFont);
            b.AddData(Xn.Color, DefaultColor);
            if (!SendTracked(userId, MessageType.Message, b, id))
            {
                _pending.RemoveAll(p => p.MsgId == id);
                Emit(new MessengerEvent.MessageFailed(userId, id));
            }
        });
        return id;
    }

    public void SendBroadcastText(IEnumerable<string> userIds, string text)
    {
        var ids = userIds.ToList();
        Post(() =>
        {
            long id = NextId();
            foreach (var uid in ids) Transmit(uid, MessageType.Broadcast, id, new XmlMessage().AddData(Xn.Broadcast, text));
        });
    }

    public void SendPublicText(string text) => Post(() =>
    {
        long id = NextId();
        foreach (var uid in _users.Keys.ToList())
        {
            var b = new XmlMessage();
            b.AddData(Xn.Font, DefaultFont);
            b.AddData(Xn.Color, DefaultColor);
            b.AddData(Xn.Message, text);
            Transmit(uid, MessageType.PublicMessage, id, b);
        }
    });

    public void SendChatState(string userId, ChatStateKind state, string threadId = "") => Post(() =>
    {
        if (!_users.ContainsKey(userId)) return;
        var b = new XmlMessage();
        if (threadId.Length > 0) b.AddData(Xn.Thread, threadId);
        b.AddData(Xn.ChatState, state.Name());
        Transmit(userId, MessageType.ChatState, NextId(), b);
    });

    public void RequestUserInfo(string userId) => Post(() =>
    {
        long id = NextId();
        SendTracked(userId, MessageType.Query, new XmlMessage().AddData(Xn.QueryOp, QueryOp.Get.Name()), id);
    });

    public void Refresh() => Post(() => { Broadcast(MessageType.Announce); KeepAlive(); });

    // ---- local profile ------------------------------------------------------------------------

    private void UpdateLocal(Func<User, User> f) => Volatile.Write(ref _localUser, f(_localUser));

    public void SetStatus(string status)
    {
        if (!Proto.StatusCodes.Contains(status)) return;
        Post(() =>
        {
            UpdateLocal(u => u with { Status = status });
            long id = NextId();
            foreach (var uid in _users.Keys.ToList()) Transmit(uid, MessageType.Status, id, new XmlMessage().AddData(Xn.Status, status));
        });
    }

    public void SetName(string name)
    {
        var n = name.Trim();
        if (n.Length == 0) return;
        Post(() =>
        {
            UpdateLocal(u => u with { Name = n });
            long id = NextId();
            foreach (var uid in _users.Keys.ToList()) Transmit(uid, MessageType.UserName, id, new XmlMessage().AddData(Xn.Name, n));
        });
    }

    public void SetNote(string note) => Post(() =>
    {
        UpdateLocal(u => u with { Note = note });
        long id = NextId();
        foreach (var uid in _users.Keys.ToList()) Transmit(uid, MessageType.Note, id, new XmlMessage().AddData(Xn.Note, note));
    });

    /// <param name="png">A PNG image file; it is announced to every online user.</param>
    public void SetLocalAvatar(string? png) => Post(() =>
    {
        _localAvatar = png;
        UpdateLocal(u => u with { AvatarPath = png });
        if (png != null && File.Exists(png)) foreach (var uid in _users.Keys.ToList()) SendAvatarTo(uid);
    });

    // ==========================================================================================
    // receiving: messages
    // ==========================================================================================

    private void ProcessMessage(Conn conn, XmlMessage m)
    {
        var from = m.Header(Xn.From);
        if (from.Length == 0) return;
        var type = Wire.ParseMessageType(m.Header(Xn.Type));
        if (type == null) return;
        long id = long.TryParse(m.Header(Xn.MessageId), out var idv) ? idv : 0;
        switch (type)
        {
            case MessageType.UserData:
                if (m.Data(Xn.QueryOp) == QueryOp.Get.Name()) SendUserData(from, QueryOp.Result);
                AddUser(
                    m.Data(Xn.UserId) is { Length: > 0 } uid ? uid : from, m.Data(Xn.Version),
                    m.Data(Xn.Address) is { Length: > 0 } addr ? addr : conn.PeerAddress,
                    m.Data(Xn.Name), m.Data(Xn.Status), m.Data(Xn.Note), int.TryParse(m.Data(Xn.UserCaps), out var caps) ? caps : 0);
                break;
            case MessageType.Broadcast:
                Emit(new MessengerEvent.BroadcastMessage(from, m.Data(Xn.Broadcast), Now()));
                break;
            case MessageType.Status: UpdateUser(from, u => u with { Status = m.Data(Xn.Status) }); break;
            case MessageType.UserName: UpdateUser(from, u => u with { Name = m.Data(Xn.Name) }); break;
            case MessageType.Note: UpdateUser(from, u => u with { Note = m.Data(Xn.Note) }); break;
            case MessageType.Message:
                if (RememberReceived(id, from)) Emit(new MessengerEvent.TextMessage(from, m.Data(Xn.Message), Now(), id));
                Ack(from, id);
                break;
            case MessageType.GroupMessage: ProcessGroupMessage(from, m); break;
            case MessageType.PublicMessage: Emit(new MessengerEvent.PublicMessage(from, m.Data(Xn.Message), Now())); break;
            case MessageType.Ping: Ack(from, id); break;
            case MessageType.Query:
                if (m.Data(Xn.QueryOp) == QueryOp.Get.Name())
                {
                    var u = LocalUser;
                    var b = new XmlMessage();
                    b.AddData(Xn.MessageId, id.ToString());
                    b.AddData(Xn.QueryOp, QueryOp.Result.Name());
                    b.AddData(Xn.UserId, LocalId);
                    b.AddData(Xn.Name, u.Name);
                    b.AddData(Xn.Address, u.Address);
                    b.AddData(Xn.Version, u.Version);
                    b.AddData(Xn.Status, u.Status);
                    b.AddData(Xn.Note, u.Note);
                    b.AddData(Xn.Logon, _cfg.LogonName);
                    b.AddData(Xn.Host, _cfg.HostName);
                    b.AddData(Xn.Os, _cfg.OsName);
                    b.AddData(Xn.FirstName, _cfg.FirstName.Length > 0 ? _cfg.FirstName : "N/A");
                    b.AddData(Xn.LastName, _cfg.LastName.Length > 0 ? _cfg.LastName : "N/A");
                    b.AddData(Xn.About, _cfg.About.Length > 0 ? _cfg.About : "N/A");
                    Transmit(from, MessageType.Query, NextId(), b);
                }
                else
                {
                    long qid = long.TryParse(m.Data(Xn.MessageId), out var qv) ? qv : -1;
                    _pending.RemoveAll(p => p.MsgId == qid && p.UserId == from);
                    var info = new Dictionary<string, string>();
                    foreach (var (k, v) in m.Body) info[k] = v;
                    Emit(new MessengerEvent.UserInfo(from, info));
                }
                break;
            case MessageType.ChatState:
                if (Wire.ParseChatState(m.Data(Xn.ChatState)) is { } st) Emit(new MessengerEvent.ChatStateChanged(from, st, m.Data(Xn.Thread)));
                break;
            case MessageType.Acknowledge:
                if (!long.TryParse(m.Data(Xn.MessageId), out var ackId)) return;
                var p = _pending.FirstOrDefault(x => x.MsgId == ackId && x.UserId == from);
                if (p == null) return;
                _pending.Remove(p);
                if (p.Type == MessageType.Message) Emit(new MessengerEvent.MessageDelivered(from, ackId));
                break;
            case MessageType.File:
            case MessageType.Avatar:
                ProcessFile(from, type.Value, m);
                break;
            case MessageType.Folder:
                ProcessFolder(from, m);
                break;
        }
    }

    private bool RememberReceived(long id, string from)
    {
        var key = $"{from}#{id}";
        if (!_received.Add(key)) return false;
        _receivedOrder.AddLast(key);
        if (_received.Count > 4000)
            for (int i = 0; i < 1000 && _receivedOrder.First != null; i++)
            {
                _received.Remove(_receivedOrder.First.Value);
                _receivedOrder.RemoveFirst();
            }
        return true;
    }

    // ---- users --------------------------------------------------------------------------------

    private void RefreshSnapshot() => _snapshot = _users.Values.ToList();

    private void AddUser(string id, string version, string address, string name, string status, string note, int caps)
    {
        if (id == LocalId || _users.ContainsKey(id)) return;
        var st = status.Length == 0 ? Proto.StatusOnline : status;
        string? avatar = null;
        var cached = Path.Combine(_cfg.CacheDir, $"avt_{SafeName(id)}.png");
        if (File.Exists(cached)) avatar = cached;
        var u = new User(id, version, address, name.Length == 0 ? id : name, st, note, caps, avatar);
        _users[id] = u;
        RefreshSnapshot();
        _log($"user added: {id} ({name}) {version} @{address}");
        Emit(new MessengerEvent.UserOnline(u));
        if (_localAvatar != null && File.Exists(_localAvatar)) SendAvatarTo(id);
    }

    private void UpdateUser(string id, Func<User, User> change)
    {
        if (!_users.TryGetValue(id, out var old)) return;
        var nu = change(old);
        if (nu == old) return;
        _users[id] = nu;
        RefreshSnapshot();
        Emit(new MessengerEvent.UserUpdated(nu));
    }

    private void RemoveUser(string id, bool close = false)
    {
        _users.Remove(id, out var u);
        if (close)
        {
            if (_streams.Remove(id, out var c)) c.Close();
            _keys.Remove(id);
        }
        _pending.RemoveAll(p => p.UserId == id);
        if (u == null) return;
        RefreshSnapshot();
        Emit(new MessengerEvent.UserOffline(id, u.Name));
        foreach (var (thread, parts) in _rooms.ToList())
        {
            if (parts.Remove(id))
            {
                Emit(new MessengerEvent.RoomNotice(thread, id, false, Now()));
                PublishRoom(thread);
            }
        }
        foreach (var t in _transfers.Values.ToList())
            if (t.UserId == id && t.State != TransferState.Complete) CancelTransferLocally(t, TransferState.Error);
    }

    // ==========================================================================================
    // group chat
    // ==========================================================================================

    public string CreateGroupChat(IEnumerable<string> userIds)
    {
        var thread = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var ids = userIds.ToList();
        Post(() =>
        {
            CreateLocalRoom(thread);
            foreach (var uid in ids) if (uid != LocalId) SendGroupOp(uid, thread, GroupMsgOp.Request);
        });
        return thread;
    }

    public void InviteToRoom(string threadId, IEnumerable<string> userIds)
    {
        var ids = userIds.ToList();
        Post(() =>
        {
            if (!_rooms.TryGetValue(threadId, out var parts)) return;
            foreach (var uid in ids) if (uid != LocalId && !parts.Contains(uid)) SendGroupOp(uid, threadId, GroupMsgOp.Request);
        });
    }

    public void SendGroupText(string threadId, string text) => Post(() =>
    {
        if (!_rooms.TryGetValue(threadId, out var parts)) return;
        long id = NextId();
        foreach (var uid in parts.ToList())
        {
            if (uid == LocalId) continue;
            var b = new XmlMessage();
            b.AddData(Xn.Font, DefaultFont);
            b.AddData(Xn.Color, DefaultColor);
            b.AddData(Xn.Message, text);
            b.AddData(Xn.Thread, threadId);
            b.AddData(Xn.GroupMsgOp, GroupMsgOp.Message.Name());
            Transmit(uid, MessageType.GroupMessage, id, b);
        }
    });

    public void LeaveGroupChat(string threadId) => Post(() =>
    {
        if (!_rooms.Remove(threadId)) return;
        PublishRoom(threadId);
        foreach (var uid in _users.Keys.ToList()) SendGroupOp(uid, threadId, GroupMsgOp.Leave);
        Emit(new MessengerEvent.RoomClosed(threadId));
    });

    public IReadOnlyDictionary<string, IReadOnlyList<string>> RoomSnapshot()
    {
        lock (_roomsLock) return new Dictionary<string, IReadOnlyList<string>>(_roomsSnapshot);
    }

    private void PublishRoom(string thread)
    {
        _rooms.TryGetValue(thread, out var parts);
        lock (_roomsLock)
        {
            if (parts == null) _roomsSnapshot.Remove(thread);
            else _roomsSnapshot[thread] = parts.ToList();
        }
        if (parts != null) Emit(new MessengerEvent.RoomChanged(new RoomInfo(thread, parts.ToList())));
    }

    private void SendGroupOp(string userId, string thread, GroupMsgOp op)
    {
        var b = new XmlMessage();
        b.AddData(Xn.Thread, thread);
        b.AddData(Xn.GroupMsgOp, op.Name());
        Transmit(userId, MessageType.GroupMessage, NextId(), b);
    }

    private void CreateLocalRoom(string thread)
    {
        _rooms[thread] = new List<string> { LocalId };
        PublishRoom(thread);
        foreach (var uid in _users.Keys.ToList()) SendGroupOp(uid, thread, GroupMsgOp.Join);
    }

    private bool AddRoomParticipant(string thread, string uid)
    {
        if (!_rooms.TryGetValue(thread, out var parts)) return false;
        // peers older than 1.2.10 predate group chat and would not understand the traffic
        if (_users.TryGetValue(uid, out var u) && uid != LocalId && Proto.CompareVersions(u.Version, "1.2.10") <= 0) return false;
        if (parts.Contains(uid)) return false;
        parts.Add(uid);
        PublishRoom(thread);
        return true;
    }

    private void ProcessGroupMessage(string from, XmlMessage m)
    {
        var thread = m.Data(Xn.Thread);
        if (thread.Length == 0) return;
        if (Wire.ParseGroupOp(m.Data(Xn.GroupMsgOp)) is not { } op) return;
        if (op == GroupMsgOp.Request)
        {
            if (!_rooms.ContainsKey(thread)) CreateLocalRoom(thread);
            AddRoomParticipant(thread, from);
            return;
        }
        if (!_rooms.TryGetValue(thread, out var parts)) return;
        switch (op)
        {
            case GroupMsgOp.Join:
                if (AddRoomParticipant(thread, from)) Emit(new MessengerEvent.RoomNotice(thread, from, true, Now()));
                break;
            case GroupMsgOp.Message:
                Emit(new MessengerEvent.RoomMessage(thread, from, m.Data(Xn.Message), Now()));
                break;
            case GroupMsgOp.Leave:
                if (parts.Remove(from))
                {
                    PublishRoom(thread);
                    Emit(new MessengerEvent.RoomNotice(thread, from, false, Now()));
                }
                break;
        }
    }

    // ==========================================================================================
    // file transfer (files, avatars) and folder decline
    // ==========================================================================================

    public string SendFile(string userId, IFileSource source)
    {
        var fileId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        Post(() => RequestSend(userId, fileId, source, FileType.Normal));
        return fileId;
    }

    private void SendAvatarTo(string userId)
    {
        var f = _localAvatar;
        if (f == null || !File.Exists(f)) return;
        RequestSend(userId, Guid.NewGuid().ToString("N").ToUpperInvariant(), new LocalFileSource(f), FileType.Avatar);
    }

    private void RequestSend(string userId, string fileId, IFileSource source, FileType type)
    {
        if (!_users.ContainsKey(userId)) return;
        var t = new Transfer
        {
            Id = fileId, UserId = userId, Name = source.Name, Size = source.Size, Outgoing = true, Type = type,
            State = TransferState.Requested, Source = source,
        };
        _transfers[fileId] = t;
        var b = new XmlMessage();
        b.AddData(Xn.FileType, type.Name());
        b.AddData(Xn.FileOp, FileOp.Request.Name());
        b.AddData(Xn.FileId, fileId);
        b.AddData(Xn.Mode, FileMode.Send.Name());
        b.AddData(Xn.FileName, source.Name);
        b.AddData(Xn.FileSize, source.Size.ToString());
        Transmit(userId, type == FileType.Avatar ? MessageType.Avatar : MessageType.File, NextId(), b);
        if (type == FileType.Normal) EmitTransfer(t);
    }

    /// <summary>Accept an incoming file offer; the data is written through <paramref name="sink"/>.</summary>
    public void AcceptFile(string userId, string fileId, IFileSink sink) => Post(() =>
    {
        if (!_transfers.TryGetValue(fileId, out var t) || t.Outgoing || t.UserId != userId) return;
        t.Sink = sink;
        t.State = TransferState.Transferring;
        var b = new XmlMessage();
        b.AddData(Xn.Mode, FileMode.Receive.Name());
        b.AddData(Xn.FileType, t.Type.Name());
        b.AddData(Xn.FileOp, FileOp.Accept.Name());
        b.AddData(Xn.FileId, fileId);
        b.AddData(Xn.FileName, t.Name);
        b.AddData(Xn.FileSize, t.Size.ToString());
        Transmit(userId, t.Type == FileType.Avatar ? MessageType.Avatar : MessageType.File, NextId(), b);
        EmitTransfer(t);
    });

    public void DeclineFile(string userId, string fileId) => Post(() =>
    {
        if (!_transfers.TryRemove(fileId, out var t)) return;
        var b = new XmlMessage();
        b.AddData(Xn.Mode, FileMode.Receive.Name());
        b.AddData(Xn.FileType, t.Type.Name());
        b.AddData(Xn.FileOp, FileOp.Decline.Name());
        b.AddData(Xn.FileId, fileId);
        Transmit(userId, MessageType.File, NextId(), b);
        t.State = TransferState.Declined;
        EmitTransfer(t);
    });

    public void CancelFile(string userId, string fileId) => Post(() =>
    {
        if (!_transfers.TryGetValue(fileId, out var t)) return;
        var b = new XmlMessage();
        b.AddData(Xn.Mode, (t.Outgoing ? FileMode.Send : FileMode.Receive).Name());
        b.AddData(Xn.FileType, t.Type.Name());
        b.AddData(Xn.FileOp, FileOp.Cancel.Name());
        b.AddData(Xn.FileId, fileId);
        Transmit(userId, MessageType.File, NextId(), b);
        CancelTransferLocally(t, TransferState.Cancelled);
    });

    private void EmitTransfer(Transfer t, string? location = null)
    {
        if (t.Type != FileType.Normal) return;
        Emit(new MessengerEvent.Transfer(new TransferInfo(t.Id, t.UserId, t.Name, t.Size, Interlocked.Read(ref t.Bytes), t.Outgoing, t.State, location)));
    }

    private void CancelTransferLocally(Transfer t, TransferState state)
    {
        t.Cancelled = true;
        try { t.Socket?.Close(); } catch (Exception) { }
        try { t.Sink?.Discard(); } catch (Exception) { }
        _transfers.TryRemove(t.Id, out _);
        if (t.State != TransferState.Complete)
        {
            t.State = state;
            EmitTransfer(t);
        }
    }

    private void ProcessFile(string from, MessageType type, XmlMessage m)
    {
        var ft = Wire.ParseFileType(m.Data(Xn.FileType)) ?? FileType.Normal;
        if (Wire.ParseFileOp(m.Data(Xn.FileOp)) is not { } op) return;
        var fileId = m.Data(Xn.FileId);
        if (fileId.Length != Proto.FileIdLength) return;
        switch (op)
        {
            case FileOp.Request:
            {
                if (ft == FileType.Folder)
                {
                    DeclineFolder(from, fileId, m.Data(Xn.FolderId) is { Length: > 0 } f ? f : fileId);
                    return;
                }
                long size = long.TryParse(m.Data(Xn.FileSize), out var sz) ? sz : 0;
                var name = SanitizeFileName(m.Data(Xn.FileName));
                var t = new Transfer { Id = fileId, UserId = from, Name = name, Size = size, Outgoing = false, Type = ft, State = TransferState.Requested };
                _transfers[fileId] = t;
                if (ft == FileType.Avatar)
                {
                    // avatars are accepted silently into the cache directory
                    if (size is > 0 and <= 2 * 1024 * 1024)
                    {
                        t.Sink = new LocalFileSink(Path.Combine(_cfg.CacheDir, $"avt_{SafeName(from)}.png"));
                        t.State = TransferState.Transferring;
                        var b = new XmlMessage();
                        b.AddData(Xn.Mode, FileMode.Receive.Name());
                        b.AddData(Xn.FileType, FileType.Avatar.Name());
                        b.AddData(Xn.FileOp, FileOp.Accept.Name());
                        b.AddData(Xn.FileId, fileId);
                        b.AddData(Xn.FileSize, size.ToString());
                        Transmit(from, MessageType.Avatar, NextId(), b);
                    }
                    else _transfers.TryRemove(fileId, out _);
                }
                else EmitTransfer(t);
                break;
            }
            case FileOp.Accept:
            {
                if (!_transfers.TryGetValue(fileId, out var t) || !t.Outgoing || t.UserId != from) return;
                t.State = TransferState.Transferring;
                EmitTransfer(t);
                var addr = _streams.TryGetValue(from, out var c) ? c.PeerAddress : _users.GetValueOrDefault(from)?.Address;
                if (addr == null) return;
                // The original client's receiver reads at most 64 KiB per readyRead() and never drains a
                // backlog, so a sender that outruns it stalls on the last chunk. Peers older than 3.0.0
                // therefore get a paced stream (see PROTOCOL.md).
                bool paced = Proto.CompareVersions(_users.GetValueOrDefault(from)?.Version ?? "0.0.0", "3.0.0") < 0;
                Task.Run(() => SendFileLoop(t, addr, paced));
                break;
            }
            case FileOp.Decline:
            {
                if (!_transfers.TryRemove(fileId, out var t)) return;
                t.State = TransferState.Declined;
                EmitTransfer(t);
                break;
            }
            case FileOp.Cancel:
            case FileOp.Abort:
            {
                if (!_transfers.TryGetValue(fileId, out var t) || t.UserId != from) return;
                CancelTransferLocally(t, TransferState.Cancelled);
                break;
            }
        }
    }

    private void ProcessFolder(string from, XmlMessage m)
    {
        if (Wire.ParseFileOp(m.Data(Xn.FileOp)) != FileOp.Request) return;
        var folderId = m.Data(Xn.FolderId) is { Length: > 0 } f ? f : m.Data(Xn.FileId);
        DeclineFolder(from, folderId, folderId);
    }

    /// <summary>Folder transfer is not supported; politely decline so the sender is not left hanging.</summary>
    private void DeclineFolder(string userId, string fileId, string folderId)
    {
        var b = new XmlMessage();
        b.AddData(Xn.Mode, FileMode.Receive.Name());
        b.AddData(Xn.FileType, FileType.Folder.Name());
        b.AddData(Xn.FileOp, FileOp.Decline.Name());
        b.AddData(Xn.FileId, fileId);
        b.AddData(Xn.FolderId, folderId);
        Transmit(userId, MessageType.Folder, NextId(), b);
    }

    /// <summary>Dedicated thread: connect to the receiver, wait for START, stream the file raw.</summary>
    private void SendFileLoop(Transfer t, string ip, bool paced)
    {
        bool ok = false;
        try
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            t.Socket = s;
            if (!s.ConnectAsync(new IPEndPoint(IPAddress.Parse(ip), _cfg.TcpPort)).Wait(8000)) throw new TimeoutException("connect timed out");
            s.ReceiveTimeout = 30000;
            s.SendTimeout = 30000;
            using var net = new NetworkStream(s, ownsSocket: false);
            net.Write(Encoding.UTF8.GetBytes("FILE" + t.Id + LocalId));
            net.Flush();
            var start = new byte[5];
            net.ReadExactly(start);
            if (Encoding.ASCII.GetString(start) != "START") throw new IOException("bad START");
            var src = t.Source ?? throw new IOException("no source");
            using var fin = src.Open();
            // 65535, not 65536: the original receiver reads at most 65535 bytes per readyRead()
            var buf = new byte[FileChunk];
            long sent = 0;
            long lastReport = Now();
            while (sent < t.Size && !t.Cancelled)
            {
                int want = (int)Math.Min(buf.Length, t.Size - sent);
                int n = fin.Read(buf, 0, want);
                if (n <= 0) break;
                net.Write(buf, 0, n);
                sent += n;
                if (paced)
                {
                    net.Flush();
                    Thread.Sleep(PacedDelayMs);
                }
                if (Now() - lastReport >= 1500)
                {
                    lastReport = Now();
                    long snap = sent;
                    Post(() => { Interlocked.Exchange(ref t.Bytes, snap); EmitTransfer(t); });
                }
            }
            net.Flush();
            ok = sent == t.Size && !t.Cancelled;
            Interlocked.Exchange(ref t.Bytes, sent);
            if (ok)
            {
                // linger briefly so the receiver reads everything before the FIN
                try { s.Shutdown(SocketShutdown.Send); s.Receive(new byte[1]); } catch (Exception) { }
            }
            s.Close();
        }
        catch (Exception e)
        {
            _log($"send file {t.Name} failed: {e.Message}");
        }
        Post(() =>
        {
            if (t.Cancelled) return;
            _transfers.TryRemove(t.Id, out _);
            t.State = ok ? TransferState.Complete : TransferState.Error;
            EmitTransfer(t);
            if (!ok) AbortOnWire(t);
        });
    }

    private void AbortOnWire(Transfer t)
    {
        var b = new XmlMessage();
        b.AddData(Xn.Mode, (t.Outgoing ? FileMode.Send : FileMode.Receive).Name());
        b.AddData(Xn.FileType, t.Type.Name());
        b.AddData(Xn.FileOp, FileOp.Abort.Name());
        b.AddData(Xn.FileId, t.Id);
        Transmit(t.UserId, t.Type == FileType.Avatar ? MessageType.Avatar : MessageType.File, NextId(), b);
    }

    /// <summary>Runs on the accepted socket's thread.</summary>
    private void ReceiveFileLoop(Transfer t, Socket s)
    {
        t.Socket = s;
        bool ok = false;
        string? location = null;
        var sink = t.Sink;
        if (sink == null) { s.Close(); return; }
        try
        {
            using var net = new NetworkStream(s, ownsSocket: false);
            using (var outStream = sink.Open())
            {
                net.Write(Encoding.ASCII.GetBytes("START"));
                net.Flush();
                var buf = new byte[64 * 1024];
                long got = 0;
                long lastReport = Now();
                while (got < t.Size && !t.Cancelled)
                {
                    int n = net.Read(buf, 0, (int)Math.Min(buf.Length, t.Size - got));
                    if (n <= 0) break;
                    outStream.Write(buf, 0, n);
                    got += n;
                    if (Now() - lastReport >= 1500)
                    {
                        lastReport = Now();
                        long snap = got;
                        Post(() => { Interlocked.Exchange(ref t.Bytes, snap); EmitTransfer(t); });
                    }
                }
                Interlocked.Exchange(ref t.Bytes, got);
                ok = got == t.Size && !t.Cancelled;
                if (!ok) _log($"receive {t.Name}: stopped at {got}/{t.Size}, cancelled={t.Cancelled}");
            }
            if (ok) location = sink.Commit(); else sink.Discard();
        }
        catch (Exception e)
        {
            _log($"receive file {t.Name} failed: {e.Message}");
            try { sink.Discard(); } catch (Exception) { }
        }
        finally
        {
            try { s.Close(); } catch (Exception) { }
        }
        Post(() =>
        {
            if (t.Cancelled) return;
            _transfers.TryRemove(t.Id, out _);
            if (ok)
            {
                t.State = TransferState.Complete;
                if (t.Type == FileType.Avatar)
                {
                    UpdateUser(t.UserId, u => u with { AvatarPath = location });
                    Emit(new MessengerEvent.AvatarUpdated(t.UserId, location));
                }
                else EmitTransfer(t, location);
            }
            else
            {
                t.State = TransferState.Error;
                EmitTransfer(t);
                AbortOnWire(t);
            }
        });
    }

    // ==========================================================================================
    // helpers
    // ==========================================================================================

    public static string BuildUserId(string machineId, string logonName)
    {
        var mac = new string(machineId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (mac.Length > 12) mac = mac[..12];
        var name = new string(logonName.Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-')).ToArray());
        var id = mac + name;
        return id.Length > Proto.MaxUserIdLength ? id[..Proto.MaxUserIdLength] : id;
    }

    public static string SafeName(string s)
    {
        var r = new string(s.Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-')).ToArray());
        if (r.Length > 64) r = r[..64];
        return r.Length == 0 ? "x" : r;
    }

    public static string SanitizeFileName(string name)
    {
        var i = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        var baseName = i >= 0 ? name[(i + 1)..] : name;
        var chars = baseName.Where(c => c >= ' ' && "<>:\"|?*".IndexOf(c) < 0).ToArray();
        var s = new string(chars).Trim().Trim('.');
        if (s.Length == 0) s = "file";
        return s.Length > 180 ? s[..180] : s;
    }
}
