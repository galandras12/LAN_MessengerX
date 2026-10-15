using System.Text.Json;
using System.Text.Json.Serialization;

namespace LanMessengerX.App.Services;

public sealed class AppSettings
{
    // ---- profile -----------------------------------------------------------------------------------
    public string MachineId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "chat";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string About { get; set; } = "";
    /// <summary>Index of the stock picture (0-19), or -1 for a picture of the user's own. null: never chosen yet (a random one is picked).</summary>
    public int? StockAvatar { get; set; }

    // ---- general -----------------------------------------------------------------------------------
    public string Language { get; set; } = "system"; // system | ar_SA | ... | hu_HU | ...
    public bool AutoStart { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool TrayIcon { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = false;
    public bool SingleClickTray { get; set; } = true;
    public bool Notifications { get; set; } = true; // balloon notifications for new messages

    // ---- messages ----------------------------------------------------------------------------------
    public bool ShowEmoticons { get; set; } = true;
    public bool ShowTimestamp { get; set; } = true;
    public bool ShowDate { get; set; } = false;
    public bool AllowLinks { get; set; } = true;
    public bool PathToLink { get; set; } = true;
    public bool TrimMessages { get; set; } = true;
    public bool ClearOnClose { get; set; } = false;
    public string MessageFont { get; set; } = "Segoe UI";
    public int MessageFontSize { get; set; } = 9;
    public string MessageColor { get; set; } = "#000000";
    /// <summary>Size of the text of incoming and outgoing messages, in points.</summary>
    public int TextSize { get; set; } = 11;

    // ---- history -----------------------------------------------------------------------------------
    public bool SaveHistory { get; set; } = true;
    public string? HistoryDir { get; set; }
    public bool SaveFileHistory { get; set; } = true;

    // ---- alerts ------------------------------------------------------------------------------------
    public bool StatusAlerts { get; set; } = true;
    public bool NoBusyAlert { get; set; } = true;
    public bool NoDndAlert { get; set; } = true;
    public bool Sounds { get; set; } = true;
    public bool NoBusySound { get; set; } = true;
    public bool NoDndSound { get; set; } = true;
    public bool SoundNewMessage { get; set; } = true;
    public bool SoundUserOnline { get; set; } = true;
    public bool SoundUserOffline { get; set; } = false;
    public bool SoundNewFile { get; set; } = true;
    public bool SoundFileDone { get; set; } = true;

    // ---- network -----------------------------------------------------------------------------------
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 1;
    /// <summary>Seconds between two announcements / liveness checks.</summary>
    public int RefreshSeconds { get; set; } = 60;
    public string Multicast { get; set; } = Core.Proto.DefaultMulticast;
    public int UdpPort { get; set; } = Core.Proto.DefaultPort;
    public int TcpPort { get; set; } = Core.Proto.DefaultPort;
    public string? PreferredInterface { get; set; }
    public bool ScanSubnet { get; set; } = true;
    /// <summary>The first-run offer to open the Windows Firewall for this program was shown.</summary>
    public bool FirewallAsked { get; set; } = false;
    public List<string> ExtraBroadcasts { get; set; } = new();

    /// <summary>Before 3.1 there was a single port for UDP and TCP; read from old settings files only.</summary>
    [JsonPropertyName("Port")]
    public int? LegacyPort { get; set; }

    // ---- file transfer -----------------------------------------------------------------------------
    public bool AutoAcceptFiles { get; set; } = false;
    public bool ShowTransferWindow { get; set; } = true;
    public string? DownloadDir { get; set; }

    // ---- appearance / hotkeys ----------------------------------------------------------------------
    public string Theme { get; set; } = "system"; // system | light | dark
    public bool CompactContacts { get; set; } = false;
    public bool ContactTooltips { get; set; } = true;
    public bool SendWithEnter { get; set; } = true; // false: Ctrl+Enter sends, Enter is a new line

    // ---- contact groups ----------------------------------------------------------------------------
    public List<ContactGroup> Groups { get; set; } = new();
    public Dictionary<string, string> UserGroups { get; set; } = new();

    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 680;

    public sealed class ContactGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public bool Collapsed { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DataDir
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir)) baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            var dir = Path.Combine(baseDir, OperatingSystem.IsWindows() ? "LAN Messenger X" : "lanmessengerx");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                if (loaded.LegacyPort is > 0 and < 65536) { loaded.UdpPort = loaded.TcpPort = loaded.LegacyPort.Value; }
                loaded.LegacyPort = null;
                return loaded;
            }
        }
        catch (Exception) { }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); }
        catch (Exception) { }
    }

    [JsonIgnore]
    public string EffectiveHistoryDir => !string.IsNullOrWhiteSpace(HistoryDir) ? HistoryDir! : Path.Combine(DataDir, "history");

    [JsonIgnore]
    public string EffectiveDownloadDir
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DownloadDir)) return DownloadDir!;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dl = Path.Combine(home, "Downloads");
            return Path.Combine(Directory.Exists(dl) ? dl : home, "LAN Messenger X");
        }
    }
}
