using System.Text.Json;

namespace LanMessengerX.App.Services;

/// <summary>File transfer history (one JSON object per line).</summary>
public static class TransferLog
{
    public sealed record Entry(long Time, string Peer, string Name, long Size, bool Outgoing, string State, string? Location);

    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(AppSettings.DataDir, "transfers.jsonl");

    public static void Append(Entry e)
    {
        try { lock (Gate) File.AppendAllText(FilePath, JsonSerializer.Serialize(e) + "\n"); }
        catch (Exception) { }
    }

    public static List<Entry> Read()
    {
        var list = new List<Entry>();
        try
        {
            lock (Gate)
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadLines(FilePath))
                        try { if (JsonSerializer.Deserialize<Entry>(l) is { } e) list.Add(e); } catch (JsonException) { }
        }
        catch (Exception) { }
        list.Reverse();
        return list;
    }

    public static void Clear()
    {
        try { lock (Gate) File.Delete(FilePath); } catch (Exception) { }
    }
}
