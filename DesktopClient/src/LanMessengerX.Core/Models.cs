namespace LanMessengerX.Core;

public sealed record User(
    string Id, string Version, string Address, string Name, string Status, string Note, int Caps, string? AvatarPath = null)
{
    public bool IsOffline => Proto.IsOffline(Status);
}

public enum TransferState { Requested, Transferring, Complete, Declined, Cancelled, Error }

public sealed record TransferInfo(
    string Id, string UserId, string Name, long Size, long Bytes, bool Outgoing, TransferState State, string? Location = null,
    bool IsFolder = false, int FileCount = 0);

public sealed record RoomInfo(string ThreadId, IReadOnlyList<string> Participants);

/// <summary>Where an outgoing file comes from.</summary>
public interface IFileSource
{
    string Name { get; }
    long Size { get; }
    Stream Open();
}

public sealed class LocalFileSource : IFileSource
{
    private readonly FileInfo _file;
    public LocalFileSource(string path) => _file = new FileInfo(path);
    public string Name => _file.Name;
    public long Size => _file.Length;
    public Stream Open() => _file.OpenRead();
}

/// <summary>Where an incoming file is written. <see cref="Commit"/> returns a user visible location.</summary>
public interface IFileSink
{
    Stream Open();
    string? Commit();
    void Discard();
}

public sealed class LocalFileSink : IFileSink
{
    private readonly string _target;
    private readonly string _partial;

    public LocalFileSink(string target)
    {
        _target = target;
        _partial = target + ".part";
    }

    public Stream Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_target))!);
        return File.Create(_partial);
    }

    public string? Commit()
    {
        File.Move(_partial, _target, overwrite: true);
        return Path.GetFullPath(_target);
    }

    public void Discard()
    {
        try { File.Delete(_partial); } catch (IOException) { }
    }
}

public abstract record MessengerEvent
{
    public sealed record ConnectionChanged(bool Connected, string? Ip) : MessengerEvent;
    public sealed record UserOnline(User User) : MessengerEvent;
    public sealed record UserUpdated(User User) : MessengerEvent;
    public sealed record UserOffline(string UserId, string Name) : MessengerEvent;
    public sealed record TextMessage(string UserId, string Text, long TimeMs, long MessageId) : MessengerEvent;
    public sealed record MessageDelivered(string UserId, long MessageId) : MessengerEvent;
    public sealed record MessageFailed(string UserId, long MessageId) : MessengerEvent;
    public sealed record BroadcastMessage(string UserId, string Text, long TimeMs) : MessengerEvent;
    public sealed record PublicMessage(string UserId, string Text, long TimeMs) : MessengerEvent;
    public sealed record ChatStateChanged(string UserId, ChatStateKind State, string ThreadId) : MessengerEvent;
    public sealed record UserInfo(string UserId, IReadOnlyDictionary<string, string> Info) : MessengerEvent;
    public sealed record AvatarUpdated(string UserId, string? Path) : MessengerEvent;
    public sealed record RoomChanged(RoomInfo Room) : MessengerEvent;
    public sealed record RoomClosed(string ThreadId) : MessengerEvent;
    public sealed record RoomMessage(string ThreadId, string UserId, string Text, long TimeMs) : MessengerEvent;
    public sealed record RoomNotice(string ThreadId, string UserId, bool Joined, long TimeMs) : MessengerEvent;
    public sealed record Transfer(TransferInfo Info) : MessengerEvent;
}
