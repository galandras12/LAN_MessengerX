using System.Text.RegularExpressions;

namespace LanMessengerX.Core;

/// <summary>
/// Makes a diagnostics log safe to share: user ids / user names, IP addresses, MAC addresses and Windows profile
/// paths are replaced by numbered placeholders (the same value always gets the same number, so the story stays readable).
/// </summary>
public static class LogAnonymizer
{
    private static readonly Regex Ip = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled);
    private static readonly Regex UserId = new(@"\b[0-9A-Fa-f]{12}[A-Za-z0-9_\-]{1,20}\b", RegexOptions.Compiled);
    private static readonly Regex Mac = new(@"\b[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5}\b", RegexOptions.Compiled);
    private static readonly Regex UserPath = new(@"(?i)([A-Z]:\\Users\\|/home/|/Users/)[^\\/\s""']+", RegexOptions.Compiled);
    private static readonly Regex AddedName = new(@"(user added: \S+) \((.*?)\) ", RegexOptions.Compiled);
    private static readonly Regex Computer = new(@"(?i)\b(host|computer|machine|logon)\s*[:=]\s*\S+", RegexOptions.Compiled);

    public static string Apply(string log)
    {
        var ips = new Dictionary<string, string>();
        var ids = new Dictionary<string, string>();
        var names = new Dictionary<string, string>();
        string Map(Dictionary<string, string> d, string key, string prefix) =>
            d.TryGetValue(key, out var v) ? v : d[key] = prefix + (d.Count + 1);

        var s = UserPath.Replace(log, m => m.Groups[1].Value + "<user>");
        s = Mac.Replace(s, "<mac>");
        s = AddedName.Replace(s, m => $"{m.Groups[1].Value} (name#{Map(names, m.Groups[2].Value, "").ToString()}) ");
        s = UserId.Replace(s, m => Map(ids, m.Value, "user#"));
        s = Computer.Replace(s, m => m.Value[..(m.Value.IndexOfAny(new[] { ':', '=' }) + 1)] + " <hidden>");
        s = Ip.Replace(s, m =>
        {
            var v = m.Value;
            if (v is "0.0.0.0" or "255.255.255.255" or "239.255.100.100" || v.StartsWith("127.")) return v;
            return Map(ips, v, "ip#");
        });
        return s;
    }

    /// <summary>Counts of the interesting events - helps to read a log without reading it.</summary>
    public static string Summary(string log)
    {
        int Count(string needle) => Regex.Matches(log, Regex.Escape(needle)).Count;
        return "SUMMARY: " +
               $"announces seen={Count("UDP announce from")}, tcp incoming={Count("TCP incoming")}, tcp connect={Count("TCP connect to")}, " +
               $"key exchanges={Count("key exchange with")}, handshake failures={Count("handshake")} failed, decrypt failures={Count("could not decrypt")}, " +
               $"users added={Count("user added:")}, no user data={Count("no user data")}, scans={Count("subnet scan #")}, connect failures={Count(" failed:")}";
    }
}
