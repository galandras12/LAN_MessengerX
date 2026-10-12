using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LanMessengerX.Core;

/// <summary>
/// The LAN Messenger envelope: &lt;lmcmessage&gt;&lt;head&gt;...&lt;/head&gt;&lt;body&gt;...&lt;/body&gt;&lt;/lmcmessage&gt;.
/// Lookups follow Qt's QDom behaviour used by the original: first descendant with that tag name,
/// text of it. DOCTYPE declarations are rejected.
/// </summary>
public sealed class XmlMessage
{
    public List<KeyValuePair<string, string>> Head { get; } = new();
    public List<KeyValuePair<string, string>> Body { get; } = new();

    public XmlMessage AddHeader(string name, string value) { Head.Add(new(name, value)); return this; }
    public XmlMessage AddData(string name, string value) { Body.Add(new(name, value)); return this; }
    public string Header(string name) => Head.FirstOrDefault(p => p.Key == name).Value ?? "";
    public string Data(string name) => Body.FirstOrDefault(p => p.Key == name).Value ?? "";
    public bool DataExists(string name) => Body.Any(p => p.Key == name);

    public XmlMessage SetData(string name, string value)
    {
        Body.RemoveAll(p => p.Key == name);
        return AddData(name, value);
    }

    public XmlMessage Copy()
    {
        var m = new XmlMessage();
        m.Head.AddRange(Head);
        m.Body.AddRange(Body);
        return m;
    }

    public string ToXml()
    {
        var sb = new StringBuilder(256);
        sb.Append('<').Append(Xn.Root).Append("><head>");
        foreach (var (k, v) in Head) sb.Append('<').Append(k).Append('>').Append(XmlText.Escape(v)).Append("</").Append(k).Append('>');
        sb.Append("</head><body>");
        foreach (var (k, v) in Body) sb.Append('<').Append(k).Append('>').Append(XmlText.Escape(v)).Append("</").Append(k).Append('>');
        sb.Append("</body></").Append(Xn.Root).Append('>');
        return sb.ToString();
    }

    public override string ToString() => ToXml();

    /// <summary>Null if the text is not a valid lmcmessage document.</summary>
    public static XmlMessage? Parse(string text)
    {
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 8 * 1024 * 1024,
                CheckCharacters = false,
            };
            using var sr = new StringReader(text);
            using var reader = XmlReader.Create(sr, settings);
            doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (Exception e) when (e is XmlException or InvalidOperationException)
        {
            return null;
        }
        var root = doc.Root;
        if (root == null || root.Name.LocalName != Xn.Root) return null;
        var msg = new XmlMessage();
        Flatten(root.Descendants().FirstOrDefault(e => e.Name.LocalName == Xn.Head), msg.Head);
        Flatten(root.Descendants().FirstOrDefault(e => e.Name.LocalName == Xn.Body), msg.Body);
        return msg;
    }

    private static void Flatten(XElement? parent, List<KeyValuePair<string, string>> into)
    {
        if (parent == null) return;
        foreach (var e in parent.Descendants()) into.Add(new(e.Name.LocalName, e.Value));
    }
}

public static class XmlText
{
    /// <summary>Drops characters illegal in XML 1.0 (they would make Qt peers reject the whole message).</summary>
    public static string Sanitize(string s)
    {
        StringBuilder? sb = null;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool ok = c == '\t' || c == '\n' || c == '\r' || (c >= ' ' && c <= '퟿') || (c >= '' && c <= '�');
            if (!ok && char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                sb?.Append(c).Append(s[i + 1]);
                i++;
                continue;
            }
            if (ok) sb?.Append(c);
            else sb ??= new StringBuilder(s, 0, i, s.Length);
        }
        return sb?.ToString() ?? s;
    }

    public static string Escape(string s)
    {
        var clean = Sanitize(s);
        var sb = new StringBuilder(clean.Length + 16);
        foreach (var c in clean)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '\r': sb.Append("&#13;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
