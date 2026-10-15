using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LanMessengerX.Core;

/// <summary>The LAN interface the messenger is bound to.</summary>
public sealed record LanInterface(string Name, string Description, IPAddress Address, int PrefixLength, int Index, string MacHex)
{
    public string Ip => Address.ToString();

    /// <summary>Subnet directed broadcast address (host bits all ones).</summary>
    public IPAddress Broadcast
    {
        get
        {
            var a = Address.GetAddressBytes();
            uint ip = (uint)(a[0] << 24 | a[1] << 16 | a[2] << 8 | a[3]);
            uint mask = PrefixLength <= 0 ? 0 : uint.MaxValue << (32 - Math.Min(PrefixLength, 32));
            uint b = ip | ~mask;
            return new IPAddress(new[] { (byte)(b >> 24), (byte)(b >> 16), (byte)(b >> 8), (byte)b });
        }
    }

    public bool SameAs(LanInterface? o) => o != null && o.Name == Name && o.Address.Equals(Address);
}

public static class NetUtil
{
    private static readonly string[] SkipWords =
    {
        "loopback", "virtual", "vmware", "vbox", "virtualbox", "hyper-v", "vethernet", "docker", "veth", "br-", "virbr",
        "tun", "tap", "ppp", "bluetooth", "wireguard", "wg", "tailscale", "zerotier", "pseudo",
    };

    /// <summary>
    /// Picks the interface used for LAN discovery: up, non-loopback, IPv4, Wi-Fi/Ethernet preferred over
    /// everything else, private (RFC1918) addresses preferred over public ones, interfaces with a
    /// default gateway preferred. Returns null if there is none.
    /// </summary>
    public static LanInterface? FindLanInterface(string? preferredName = null)
    {
        LanInterface? best = null;
        int bestScore = int.MinValue;
        NetworkInterface[] all;
        try { all = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (Exception) { return null; }
        foreach (var ni in all)
        {
            try
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
                var props = ni.GetIPProperties();
                string label = (ni.Name + " " + ni.Description).ToLowerInvariant();
                int score = 0;
                if (preferredName != null && ni.Name == preferredName) score += 1000;
                if (SkipWords.Any(label.Contains)) score -= 100;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211) score += 20;
                if (props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) score += 15;
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var b = ua.Address.GetAddressBytes();
                    if (b[0] == 127 || (b[0] == 169 && b[1] == 254)) continue;
                    int s = score;
                    if (IsPrivate(b)) s += 30;
                    if (s > bestScore)
                    {
                        bestScore = s;
                        best = new LanInterface(ni.Name, ni.Description, ua.Address, PrefixOf(ua), SafeIndex(props), Hex(ni.GetPhysicalAddress().GetAddressBytes()));
                    }
                }
            }
            catch (Exception) { }
        }
        return best;
    }

    /// <summary>One line per usable IPv4 adapter address, for the diagnostics log and the interface picker.</summary>
    public static List<LanInterface> ListInterfaces()
    {
        var list = new List<LanInterface>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                    var props = ni.GetIPProperties();
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var b = ua.Address.GetAddressBytes();
                        if (b[0] == 127 || (b[0] == 169 && b[1] == 254)) continue;
                        list.Add(new LanInterface(ni.Name, ni.Description, ua.Address, PrefixOf(ua), SafeIndex(props), Hex(ni.GetPhysicalAddress().GetAddressBytes())));
                    }
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }
        return list;
    }

    /// <summary>Addresses of the interface's subnet (at most 1022; networks larger than /22 are cut down to the /24 around the address).</summary>
    public static IEnumerable<string> SubnetHosts(LanInterface lan)
    {
        var a = lan.Address.GetAddressBytes();
        uint ip = (uint)(a[0] << 24 | a[1] << 16 | a[2] << 8 | a[3]);
        int prefix = lan.PrefixLength is < 22 or > 30 ? 24 : lan.PrefixLength;
        uint mask = uint.MaxValue << (32 - prefix);
        uint net = ip & mask;
        uint bc = net | ~mask;
        for (uint h = net + 1; h < bc; h++)
            yield return $"{h >> 24}.{h >> 16 & 255}.{h >> 8 & 255}.{h & 255}";
    }

    private static int SafeIndex(IPInterfaceProperties p)
    {
        try { return p.GetIPv4Properties()?.Index ?? 0; } catch (Exception) { return 0; }
    }

    private static int PrefixOf(UnicastIPAddressInformation ua)
    {
        try
        {
            if (ua.PrefixLength > 0) return ua.PrefixLength;
            var m = ua.IPv4Mask?.GetAddressBytes();
            if (m == null) return 24;
            int n = 0;
            foreach (var by in m) for (int i = 7; i >= 0; i--) if ((by >> i & 1) == 1) n++;
            return n;
        }
        catch (Exception) { return 24; }
    }

    private static bool IsPrivate(byte[] b) =>
        b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);

    public static string Hex(byte[] b) => string.Concat(b.Select(x => x.ToString("X2")));

    /// <summary>MAC address of the first real network adapter, or null (used to derive a stable user id).</summary>
    public static string? FirstMac()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211)
                         .OrderBy(n => n.Name))
            {
                var mac = ni.GetPhysicalAddress().GetAddressBytes();
                if (mac.Length == 6 && mac.Any(x => x != 0)) return Hex(mac);
            }
        }
        catch (Exception) { }
        return null;
    }
}
