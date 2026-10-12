using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LanMessengerX.Core;

namespace LanMessengerX.App.ViewModels;

public static class StatusText
{
    public static string Label(string code) => code switch
    {
        "chat" => "Available",
        "busy" => "Busy",
        "dnd" => "Do not disturb",
        "brb" => "Be right back",
        "away" => "Away",
        "gone" => "Appear offline",
        _ => code,
    };

    public static readonly IBrush Online = new SolidColorBrush(Color.Parse("#2E9E5B"));
    public static readonly IBrush Busy = new SolidColorBrush(Color.Parse("#D32F2F"));
    public static readonly IBrush Away = new SolidColorBrush(Color.Parse("#F9A825"));
    public static readonly IBrush Offline = new SolidColorBrush(Color.Parse("#9E9E9E"));

    public static IBrush Presence(string code) =>
        Proto.IsOffline(code) ? Offline : Proto.IsBusy(code) ? Busy : Proto.IsAway(code) ? Away : Online;
}

public sealed class ContactVm : Observable
{
    public string Id { get; }
    private string _name = "";
    private string _note = "";
    private string _status = "chat";
    private Bitmap? _avatar;
    private int _unread;
    private bool _typing;

    public ContactVm(User u)
    {
        Id = u.Id;
        Apply(u);
    }

    public void Apply(User u)
    {
        Name = u.Name;
        Note = u.Note;
        Status = u.Status;
    }

    public string Name { get => _name; set { if (Set(ref _name, value)) { Raise(nameof(Subtitle)); } } }
    public string Note { get => _note; set { if (Set(ref _note, value)) Raise(nameof(Subtitle)); } }
    public string Status
    {
        get => _status;
        set { if (Set(ref _status, value)) { Raise(nameof(Presence)); Raise(nameof(Subtitle)); } }
    }
    public Bitmap? Avatar { get => _avatar; set => Set(ref _avatar, value); }
    public int Unread { get => _unread; set { if (Set(ref _unread, value)) { Raise(nameof(HasUnread)); Raise(nameof(UnreadText)); } } }
    public bool Typing { get => _typing; set { if (Set(ref _typing, value)) Raise(nameof(Subtitle)); } }

    public bool HasUnread => _unread > 0;
    public string UnreadText => _unread > 99 ? "99+" : _unread.ToString();
    public IBrush Presence => StatusText.Presence(_status);
    public string Subtitle => _typing ? "typing…" : !string.IsNullOrWhiteSpace(_note) ? _note : StatusText.Label(_status);
}

public sealed class StatusChoice
{
    public string Code { get; }
    public string Label => StatusText.Label(Code);
    public StatusChoice(string code) => Code = code;
    public override string ToString() => Label;
}
