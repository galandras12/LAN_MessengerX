using System.Text.Json;
using System.Text.Json.Serialization;

namespace LanMessengerX.App.Services;

public sealed class AppSettings
{
    public string MachineId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "chat";
    public bool SaveHistory { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public string Theme { get; set; } = "system"; // system | light | dark
    public string? DownloadDir { get; set; }
    public int Port { get; set; } = Core.Proto.DefaultPort;
    public string? PreferredInterface { get; set; }
    public List<string> ExtraBroadcasts { get; set; } = new();
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 680;

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
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
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
