using LanMessengerX.Core;

// Headless command line client used for interoperability testing against the original LAN Messenger
// (see Protocol/interop). Commands on stdin:
//   msg <userId> <text...> | file <userId> <path> | accept <userId> <fileId> <savePath>
//   avatar <png> | status <code> | group <userId,userId,..> | gsay <thread> <text...>
//   query <userId> | users | quit
var name = args.Length > 0 ? args[0] : "csharp";
var mac = args.Length > 1 ? args[1] : "AABBCCDDEE02";
var dir = Path.Combine(Path.GetTempPath(), "lmx-cs-cli-" + name);
Directory.CreateDirectory(dir);
var cfg = new MessengerConfig
{
    UserName = name, MachineId = mac, LogonName = name, HostName = "cli-" + name, OsName = "Linux CLI",
    CacheDir = Path.Combine(dir, "cache"),
};
Directory.CreateDirectory(cfg.CacheDir);
var gate = new object();
void Out(string s) { lock (gate) { Console.Out.WriteLine(s); Console.Out.Flush(); } }
using var m = new Messenger(cfg, s => Out("LOG " + s), e => Out("EVT " + Describe(e)));
m.Start();
Out("READY id=" + m.LocalId);
string? line;
while ((line = Console.In.ReadLine()) != null)
{
    var p = line.Trim().Split(' ');
    switch (p[0])
    {
        case "msg": Out("SENT id=" + m.SendText(p[1], string.Join(' ', p.Skip(2)))); break;
        case "file": Out("FILEID " + m.SendFile(p[1], new LocalFileSource(p[2]))); break;
        case "accept": m.AcceptFile(p[1], p[2], new LocalFileSink(p[3])); break;
        case "avatar": m.SetLocalAvatar(p[1]); break;
        case "status": m.SetStatus(p[1]); break;
        case "group": Out("THREAD " + m.CreateGroupChat(p[1].Split(','))); break;
        case "gsay": m.SendGroupText(p[1], string.Join(' ', p.Skip(2))); break;
        case "query": m.RequestUserInfo(p[1]); break;
        case "users": Out("USERS " + string.Join(", ", m.OnlineUsers.Select(u => u.Id + "=" + u.Name))); break;
        case "quit": m.Stop(); return;
    }
}
m.Stop();

// Mirrors the Kotlin CLI's output format so the same interop scenarios run against both.
static string Describe(MessengerEvent e) => e switch
{
    MessengerEvent.ConnectionChanged c => $"ConnectionChanged(connected={c.Connected}, ip={c.Ip})",
    MessengerEvent.UserOnline u => $"UserOnline(user=User(id={u.User.Id}, version={u.User.Version}, address={u.User.Address}, name={u.User.Name}, status={u.User.Status}, note={u.User.Note}, caps={u.User.Caps}, avatarPath={u.User.AvatarPath}))",
    MessengerEvent.UserUpdated u => $"UserUpdated(user=User(id={u.User.Id}, version={u.User.Version}, address={u.User.Address}, name={u.User.Name}, status={u.User.Status}, note={u.User.Note}, caps={u.User.Caps}, avatarPath={u.User.AvatarPath}))",
    MessengerEvent.UserOffline u => $"UserOffline(userId={u.UserId}, name={u.Name})",
    MessengerEvent.TextMessage t => $"TextMessage(userId={t.UserId}, text={t.Text}, timeMs={t.TimeMs}, messageId={t.MessageId})",
    MessengerEvent.MessageDelivered d => $"MessageDelivered(userId={d.UserId}, messageId={d.MessageId})",
    MessengerEvent.MessageFailed f => $"MessageFailed(userId={f.UserId}, messageId={f.MessageId})",
    MessengerEvent.BroadcastMessage b => $"BroadcastMessage(userId={b.UserId}, text={b.Text})",
    MessengerEvent.UserInfo i => $"UserInfo(userId={i.UserId}, info={{{string.Join(", ", i.Info.Select(kv => kv.Key + "=" + kv.Value))}}})",
    MessengerEvent.AvatarUpdated a => $"AvatarUpdated(userId={a.UserId}, path={a.Path})",
    MessengerEvent.RoomChanged r => $"RoomChanged(room=RoomInfo(threadId={r.Room.ThreadId}, participants=[{string.Join(", ", r.Room.Participants)}]))",
    MessengerEvent.RoomClosed r => $"RoomClosed(threadId={r.ThreadId})",
    MessengerEvent.RoomMessage r => $"RoomMessage(threadId={r.ThreadId}, userId={r.UserId}, text={r.Text}, timeMs={r.TimeMs})",
    MessengerEvent.RoomNotice r => $"RoomNotice(threadId={r.ThreadId}, userId={r.UserId}, joined={r.Joined.ToString().ToLowerInvariant()}, timeMs={r.TimeMs})",
    MessengerEvent.Transfer t => $"Transfer(info=TransferInfo(id={t.Info.Id}, userId={t.Info.UserId}, name={t.Info.Name}, size={t.Info.Size}, bytes={t.Info.Bytes}, outgoing={t.Info.Outgoing.ToString().ToLowerInvariant()}, state={t.Info.State.ToString().ToUpperInvariant()}, location={t.Info.Location}))",
    _ => e.ToString() ?? "",
};
