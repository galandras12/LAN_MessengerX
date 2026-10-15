using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace LanMessengerX.Core;

/// <summary>One framed TCP message stream ("MSG" connection) to a peer.</summary>
internal sealed class Conn
{
    public Socket Socket { get; }
    public bool Incoming { get; }
    public string PeerId { get; set; }
    public string PeerAddress { get; }
    public SessionCipher? Cipher { get; set; }
    public volatile bool Closed;

    /// <summary>Opened by the subnet scan: the peer's user id is not known until its first message arrives.</summary>
    public bool Probe { get; set; }

    private readonly NetworkStream _stream;
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    public Conn(Socket socket, bool incoming, string peerId)
    {
        Socket = socket;
        Incoming = incoming;
        PeerId = peerId;
        PeerAddress = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "";
        _stream = new NetworkStream(socket, ownsSocket: false);
        _ = Task.Run(WriteLoop);
    }

    private async Task WriteLoop()
    {
        try
        {
            await foreach (var chunk in _outbox.Reader.ReadAllAsync())
            {
                await _stream.WriteAsync(chunk);
                await _stream.FlushAsync();
            }
        }
        catch (Exception)
        {
            Close();
        }
    }

    /// <summary>Raw, unframed bytes (only used for the leading "MSG"+id packet).</summary>
    public void SendRaw(byte[] data) => _outbox.Writer.TryWrite(data);

    /// <summary>4 byte big endian length, then 6 byte type tag, then data.</summary>
    public void SendFrame(DatagramType type, byte[] data)
    {
        var tag = System.Text.Encoding.ASCII.GetBytes(type.Tag());
        int len = tag.Length + data.Length;
        var buf = new byte[4 + len];
        buf[0] = (byte)(len >> 24);
        buf[1] = (byte)(len >> 16);
        buf[2] = (byte)(len >> 8);
        buf[3] = (byte)len;
        Buffer.BlockCopy(tag, 0, buf, 4, tag.Length);
        Buffer.BlockCopy(data, 0, buf, 4 + tag.Length, data.Length);
        SendRaw(buf);
    }

    /// <summary>Blocks until a whole frame arrived; null on EOF or protocol violation.</summary>
    public byte[]? ReadFrame()
    {
        try
        {
            var head = new byte[4];
            _stream.ReadExactly(head);
            int len = head[0] << 24 | head[1] << 16 | head[2] << 8 | head[3];
            if (len < 0 || len > Proto.MaxFrameSize) return null;
            var buf = new byte[len];
            _stream.ReadExactly(buf);
            return buf;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Close()
    {
        if (Closed) return;
        Closed = true;
        _outbox.Writer.TryComplete();
        try { Socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
        try { Socket.Close(); } catch (Exception) { }
    }
}

/// <summary>UDP discovery socket pair: a wildcard bound receiver and an interface bound sender.</summary>
internal sealed class UdpService
{
    private readonly LanInterface _lan;
    private readonly int _port;
    private readonly IPAddress _group;
    private readonly Action<string, string> _onDatagram;
    private readonly Action<string> _log;
    private Socket? _receiver;
    private Socket? _sender;
    private volatile bool _running;

    public UdpService(LanInterface lan, int port, IPAddress group, Action<string, string> onDatagram, Action<string> log)
    {
        _lan = lan;
        _port = port;
        _group = group;
        _onDatagram = onDatagram;
        _log = log;
    }

    public void Start()
    {
        _running = true;
        try
        {
            var r = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            r.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            r.Bind(new IPEndPoint(IPAddress.Any, _port));
            try { r.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_group, _lan.Address)); }
            catch (Exception e) { _log($"Multicast join failed: {e.Message} (broadcast discovery still works)"); }
            _receiver = r;
            _log($"UDP listening on 0.0.0.0:{_port}, multicast {_group} via {_lan.Address}");
            new Thread(() => ReceiveLoop(r)) { IsBackground = true, Name = "lmx-udp" }.Start();
        }
        catch (Exception e)
        {
            _log($"UDP bind to port {_port} failed: {e.Message}");
        }
        try
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(_lan.Address, 0));
            s.EnableBroadcast = true;
            try
            {
                s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, _lan.Address.GetAddressBytes());
                s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            }
            catch (Exception) { }
            _sender = s;
            _log($"UDP sender bound to {_lan.Address}");
        }
        catch (Exception e)
        {
            _log($"UDP sender setup failed: {e.Message}");
        }
    }

    private void ReceiveLoop(Socket r)
    {
        var buf = new byte[65535];
        EndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                int n = r.ReceiveFrom(buf, ref any);
                var from = ((IPEndPoint)any).Address.ToString();
                _onDatagram(from, System.Text.Encoding.UTF8.GetString(buf, 0, n));
            }
            catch (SocketException e) when (_running && e.SocketErrorCode is SocketError.ConnectionReset or SocketError.MessageSize or SocketError.NetworkReset or SocketError.HostUnreachable or SocketError.NetworkUnreachable)
            {
                // Windows reports ICMP "port unreachable" of earlier sends on the receiving socket - harmless, keep listening
                _log($"UDP receive hiccup ignored: {e.SocketErrorCode}");
            }
            catch (Exception e)
            {
                if (_running) _log($"UDP receive error: {e.Message}");
                break;
            }
        }
    }

    public void Send(byte[] data, IEnumerable<IPAddress> targets)
    {
        var s = _sender;
        if (s == null) return;
        foreach (var t in targets)
        {
            try { s.SendTo(data, new IPEndPoint(t, _port)); }
            catch (Exception e) { _log($"UDP send to {t} failed: {e.Message}"); }
        }
    }

    public void Stop()
    {
        _running = false;
        try { _receiver?.Close(); } catch (Exception) { }
        try { _sender?.Close(); } catch (Exception) { }
        _receiver = null;
        _sender = null;
    }
}

internal sealed class TcpServer
{
    private readonly int _port;
    private readonly Action<Socket> _onSocket;
    private readonly Action<string> _log;
    private Socket? _server;
    private volatile bool _running;

    /// <summary>The port actually listened on (differs from the requested one if that was taken and 0 = any port was used).</summary>
    public int Port { get; private set; }

    public TcpServer(int port, Action<Socket> onSocket, Action<string> log)
    {
        _port = port;
        _onSocket = onSocket;
        _log = log;
    }

    public bool Start()
    {
        try
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // inherited by accepted sockets: a roomy window keeps the original client's file sender from stalling
            s.ReceiveBufferSize = 1 << 20;
            s.Bind(new IPEndPoint(IPAddress.Any, _port));
            s.Listen(64);
            Port = ((IPEndPoint)s.LocalEndPoint!).Port;
            _server = s;
            _running = true;
            new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        var c = s.Accept();
                        c.NoDelay = true;
                        c.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        _onSocket(c);
                    }
                    catch (Exception e)
                    {
                        if (_running) _log($"TCP accept error: {e.Message}");
                        break;
                    }
                }
            }) { IsBackground = true, Name = "lmx-accept" }.Start();
            return true;
        }
        catch (Exception e)
        {
            _log($"TCP listen on port {_port} failed: {e.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _running = false;
        try { _server?.Close(); } catch (Exception) { }
        _server = null;
    }
}
