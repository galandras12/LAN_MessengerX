using System.Windows.Input;
using Avalonia;
using Avalonia.Layout;
using LanMessengerX.Core;

namespace LanMessengerX.App.ViewModels;

public abstract class ChatItemVm : Observable
{
    public long Time { get; }
    protected ChatItemVm(long time) => Time = time;
    public string TimeText => DateTimeOffset.FromUnixTimeMilliseconds(Time).ToLocalTime().ToString("t");
    public string DayText
    {
        get
        {
            var d = DateTimeOffset.FromUnixTimeMilliseconds(Time).ToLocalTime().Date;
            var today = DateTime.Today;
            return d == today ? "Today" : d == today.AddDays(-1) ? "Yesterday" : d.ToString("D");
        }
    }

    private bool _showDay;
    public bool ShowDay { get => _showDay; set => Set(ref _showDay, value); }
}

public enum Delivery { Sending, Delivered, Failed }

public sealed class MessageVm : ChatItemVm
{
    public string Text { get; }
    public bool Outgoing { get; }
    public string Sender { get; }
    public long MessageId { get; }
    public bool ShowSender { get; }

    private Delivery _state;
    public Delivery State
    {
        get => _state;
        set { if (Set(ref _state, value)) { Raise(nameof(StateText)); Raise(nameof(HasStateText)); Raise(nameof(IsFailed)); } }
    }

    public MessageVm(string text, bool outgoing, string sender, long time, long messageId, Delivery state, bool showSender) : base(time)
    {
        Text = text;
        Outgoing = outgoing;
        Sender = sender;
        MessageId = messageId;
        ShowSender = showSender && !outgoing;
        _state = state;
    }

    public HorizontalAlignment Align => Outgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public string StateText => State switch { Delivery.Sending => "sending…", Delivery.Failed => "not delivered", _ => "✓" };
    public bool HasStateText => Outgoing;
    public bool IsFailed => State == Delivery.Failed;
}

public sealed class NoticeVm : ChatItemVm
{
    public string Text { get; }
    public NoticeVm(string text, long time) : base(time) => Text = text;
}

public sealed class BroadcastVm : ChatItemVm
{
    public string Header { get; }
    public string Text { get; }
    public BroadcastVm(string header, string text, long time) : base(time)
    {
        Header = header;
        Text = text;
    }
}

public sealed class FileVm : ChatItemVm
{
    public string FileId { get; }
    public string Name { get; }
    public long Size { get; }
    public bool Outgoing { get; }
    private readonly Action<FileVm, string> _act;

    private TransferState _state;
    private long _bytes;
    private string? _location;

    public FileVm(TransferInfo t, Action<FileVm, string> act) : base(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    {
        FileId = t.Id;
        Name = t.Name;
        Size = t.Size;
        Outgoing = t.Outgoing;
        _act = act;
        Update(t);
        AcceptCommand = new RelayCommand(() => _act(this, "accept"));
        DeclineCommand = new RelayCommand(() => _act(this, "decline"));
        CancelCommand = new RelayCommand(() => _act(this, "cancel"));
        OpenCommand = new RelayCommand(() => _act(this, "open"));
        ShowCommand = new RelayCommand(() => _act(this, "show"));
    }

    public ICommand AcceptCommand { get; }
    public ICommand DeclineCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ShowCommand { get; }

    public string? Location => _location;
    public TransferState State => _state;
    public double Progress => Size > 0 ? Math.Min(100.0, _bytes * 100.0 / Size) : 0;
    public bool ShowProgress => _state == TransferState.Transferring;
    public bool CanAccept => !Outgoing && _state == TransferState.Requested;
    public bool CanDecline => CanAccept;
    public bool CanCancel => _state == TransferState.Transferring || (_state == TransferState.Requested && Outgoing);
    public bool CanOpen => !Outgoing && _state == TransferState.Complete && _location != null;
    public bool IsError => _state == TransferState.Error;
    public string SizeText => Format(Size);

    public string StatusText => _state switch
    {
        TransferState.Requested => Outgoing ? $"Waiting for the other side to accept · {Format(Size)}" : $"Wants to send you this file · {Format(Size)}",
        TransferState.Transferring => $"{Format(_bytes)} of {Format(Size)}",
        TransferState.Complete => (Outgoing ? "Sent" : "Saved") + $" · {Format(Size)}",
        TransferState.Declined => "Declined",
        TransferState.Cancelled => "Cancelled",
        _ => "Transfer failed",
    };

    public void Update(TransferInfo t)
    {
        _state = t.State;
        _bytes = t.Bytes;
        if (t.Location != null) _location = t.Location;
        foreach (var p in new[]
                 {
                     nameof(State), nameof(Progress), nameof(ShowProgress), nameof(CanAccept), nameof(CanDecline), nameof(CanCancel),
                     nameof(CanOpen), nameof(IsError), nameof(StatusText), nameof(Location),
                 })
            Raise(p);
    }

    public static string Format(long size) => size switch
    {
        >= 1L << 30 => $"{size / (1024.0 * 1024 * 1024):0.00} GB",
        >= 1L << 20 => $"{size / (1024.0 * 1024):0.0} MB",
        >= 1L << 10 => $"{size / 1024.0:0} KB",
        _ => $"{size} B",
    };
}
