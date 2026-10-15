namespace LanMessengerX.Core;

/// <summary>
/// Constants of the LAN Messenger wire protocol. Everything here mirrors the original
/// (Qt based) LAN Messenger so that this client talks to old clients unchanged.
/// See Protocol/PROTOCOL.md for the full description.
/// </summary>
public static class Proto
{
    public const string AppMarker = "lmcmessage";
    public const int DefaultPort = 50000;
    public const string DefaultMulticast = "239.255.100.100";

    /// <summary>Must compare as newer than 1.2.30 (old clients gate group chat on &gt; 1.2.10).</summary>
    public const string Version = "3.0.0";

    public const int IdPacketMax = 64;
    public const int FileIdLength = 32;

    /// <summary>Old clients read at most 64 bytes of "FILE" + fileId(32) + userId, so keep our id short.</summary>
    public const int MaxUserIdLength = 28;

    public const int MaxFrameSize = 8 * 1024 * 1024;

    public const int UcFile = 0x1;
    public const int UcGroupMessage = 0x2;
    public const int UcFolder = 0x4;

    public static readonly string[] StatusCodes = { "chat", "busy", "dnd", "brb", "away", "gone" };
    public const string StatusOnline = "chat";
    public const string StatusOffline = "gone";

    public static bool IsOffline(string s) => s == StatusOffline;
    public static bool IsBusy(string s) => s == "busy" || s == "dnd";
    public static bool IsAway(string s) => s == "brb" || s == "away";

    /// <summary>&lt;0 if a is older than b, &gt;0 if newer. Only the first three components are compared, like the original.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var pb = b.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < 3; i++)
        {
            int x = i < pa.Length && int.TryParse(pa[i], out var xv) ? xv : 0;
            int y = i < pb.Length && int.TryParse(pb[i], out var yv) ? yv : 0;
            if (x != y) return x - y;
        }
        return 0;
    }
}

/// <summary>The six character tag in front of every framed TCP payload.</summary>
public enum DatagramType { PublicKey, Handshake, Message }

public static class DatagramTags
{
    public static string Tag(this DatagramType t) => t switch
    {
        DatagramType.PublicKey => "PUBKEY",
        DatagramType.Handshake => "HNDSHK",
        _ => "MESSAG",
    };

    public static DatagramType? FromTag(string tag) => tag switch
    {
        "PUBKEY" => DatagramType.PublicKey,
        "HNDSHK" => DatagramType.Handshake,
        "MESSAG" => DatagramType.Message,
        _ => null,
    };
}

public enum MessageType
{
    Blank, Announce, Depart, UserData, Broadcast, Status, Avatar, UserName, Ping, Message, GroupMessage,
    PublicMessage, File, Acknowledge, Failed, Error, OldVersion, Query, Info, ChatState, Note, Folder,
}

public enum FileMode { Send, Receive }
public enum FileOp { Init, Request, Accept, Decline, Cancel, Progress, Error, Abort, Complete, Next }
public enum FileType { Normal, Avatar, Folder }
public enum QueryOp { Get, Result }
public enum GroupMsgOp { Request, Join, Message, Leave }
public enum ChatStateKind { Active, Composing, Paused, Inactive, Gone }

/// <summary>Maps the protocol enums to and from their on-the-wire strings.</summary>
public static class Wire
{
    private static readonly Dictionary<MessageType, string> MsgNames = new()
    {
        [MessageType.Blank] = "", [MessageType.Announce] = "announce", [MessageType.Depart] = "depart",
        [MessageType.UserData] = "userdata", [MessageType.Broadcast] = "broadcast", [MessageType.Status] = "status",
        [MessageType.Avatar] = "avatar", [MessageType.UserName] = "name", [MessageType.Ping] = "ping",
        [MessageType.Message] = "message", [MessageType.GroupMessage] = "groupmessage",
        [MessageType.PublicMessage] = "publicmessage", [MessageType.File] = "file",
        [MessageType.Acknowledge] = "acknowledge", [MessageType.Failed] = "failed", [MessageType.Error] = "error",
        [MessageType.OldVersion] = "oldversion", [MessageType.Query] = "query", [MessageType.Info] = "info",
        [MessageType.ChatState] = "chatstate", [MessageType.Note] = "note", [MessageType.Folder] = "folder",
    };

    private static readonly Dictionary<string, MessageType> MsgByName = MsgNames.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string Name(this MessageType t) => MsgNames[t];
    public static MessageType? ParseMessageType(string s) => MsgByName.TryGetValue(s, out var t) ? t : null;

    public static string Name(this FileMode m) => m == FileMode.Send ? "send" : "receive";
    public static FileMode? ParseFileMode(string s) => s switch { "send" => FileMode.Send, "receive" => FileMode.Receive, _ => null };

    public static string Name(this FileOp o) => o.ToString().ToLowerInvariant();
    public static FileOp? ParseFileOp(string s) => Enum.TryParse<FileOp>(s, true, out var o) && Enum.IsDefined(o) ? o : null;

    public static string Name(this FileType t) => t.ToString().ToLowerInvariant();
    public static FileType? ParseFileType(string s) => Enum.TryParse<FileType>(s, true, out var o) && Enum.IsDefined(o) ? o : null;

    public static string Name(this QueryOp q) => q == QueryOp.Get ? "get" : "result";
    public static string Name(this GroupMsgOp o) => o.ToString().ToLowerInvariant();
    public static GroupMsgOp? ParseGroupOp(string s) => Enum.TryParse<GroupMsgOp>(s, true, out var o) && Enum.IsDefined(o) ? o : null;

    public static string Name(this ChatStateKind k) => k.ToString().ToLowerInvariant();
    public static ChatStateKind? ParseChatState(string s) => Enum.TryParse<ChatStateKind>(s, true, out var o) && Enum.IsDefined(o) ? o : null;

    public static FileMode Reversed(this FileMode m) => m == FileMode.Send ? FileMode.Receive : FileMode.Send;
}

/// <summary>XML element names of the message envelope.</summary>
public static class Xn
{
    public const string Root = Proto.AppMarker;
    public const string Head = "head";
    public const string Body = "body";
    public const string From = "from";
    public const string To = "to";
    public const string MessageId = "messageid";
    public const string Type = "type";
    public const string Address = "address";
    public const string UserId = "userid";
    public const string Name = "name";
    public const string Version = "version";
    public const string Status = "status";
    public const string Logon = "logon";
    public const string Host = "host";
    public const string Os = "os";
    public const string FirstName = "firstname";
    public const string LastName = "lastname";
    public const string About = "about";
    public const string Thread = "thread";
    public const string Message = "message";
    public const string Broadcast = "broadcast";
    public const string Mode = "mode";
    public const string FileOp = "fileop";
    public const string FileType = "filetype";
    public const string FileId = "fileid";
    public const string FilePath = "filepath";
    public const string FileName = "filename";
    public const string FileSize = "filesize";
    public const string ChatState = "chatstate";
    public const string QueryOp = "queryop";
    public const string Font = "font";
    public const string Color = "color";
    public const string GroupMsgOp = "groupmsgop";
    public const string Note = "note";
    public const string UserCaps = "usercaps";
    public const string FolderId = "folderid";
    public const string TcpPort = "tcpport";
}
