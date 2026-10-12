using System.Text.Json;

namespace LanMessengerX.App.Services;

/// <summary>One JSON object per line, one file per conversation.</summary>
public sealed class HistoryStore
{
    public sealed record Entry(long Time, bool Outgoing, string Sender, string Text);
    public sealed record Conversation(string Key, string Title, long LastTime, int Count);
    private sealed record Line(long T, bool O, string S, string X, string Title);

    private readonly string _dir;
    private readonly object _lock = new();

    public HistoryStore(string dataDir)
    {
        _dir = Path.Combine(dataDir, "history");
        Directory.CreateDirectory(_dir);
    }

    private string FileFor(string key) =>
        Path.Combine(_dir, new string(key.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').Take(80).ToArray()) + ".jsonl");

    public void Append(string key, string title, Entry e)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Line(e.Time, e.Outgoing, e.Sender, e.Text, title));
            lock (_lock) File.AppendAllText(FileFor(key), json + "\n");
        }
        catch (Exception) { }
    }

    public List<Conversation> Conversations()
    {
        var list = new List<Conversation>();
        lock (_lock)
        {
            foreach (var f in Directory.GetFiles(_dir, "*.jsonl"))
            {
                int n = 0; long last = 0; string title = "";
                foreach (var l in File.ReadLines(f))
                {
                    var line = Parse(l);
                    if (line == null) continue;
                    n++; last = line.T; title = line.Title;
                }
                if (n > 0) list.Add(new Conversation(Path.GetFileNameWithoutExtension(f), title, last, n));
            }
        }
        return list.OrderByDescending(c => c.LastTime).ToList();
    }

    public List<Entry> Read(string key)
    {
        var list = new List<Entry>();
        lock (_lock)
        {
            var f = FileFor(key);
            if (!File.Exists(f)) return list;
            foreach (var l in File.ReadLines(f))
            {
                var line = Parse(l);
                if (line != null) list.Add(new Entry(line.T, line.O, line.S, line.X));
            }
        }
        return list;
    }

    public void Delete(string key)
    {
        lock (_lock) { try { File.Delete(FileFor(key)); } catch (IOException) { } }
    }

    public void Clear()
    {
        lock (_lock) foreach (var f in Directory.GetFiles(_dir, "*.jsonl")) { try { File.Delete(f); } catch (IOException) { } }
    }

    private static Line? Parse(string l)
    {
        try { return JsonSerializer.Deserialize<Line>(l); }
        catch (JsonException) { return null; }
    }
}
