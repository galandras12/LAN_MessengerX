using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LanMessengerX.App.Services;

/// <summary>The smileys of the original LAN Messenger (same text codes, same pictures).</summary>
public static class Emoticons
{
    // code -> picture number (1..70); the first 70 codes are the long forms, the rest are shortcuts without the nose
    private static readonly string[] Codes =
    {
        ":-)", ":-D", ";-)", ":-O", ":-P", "(H)", ":-@", ":-S", ":-$", ":-(",
        ":'(", ":-|", "(ST)", "8o|", "8-|", "+o(", "<:o)", "|-)", "*-)", ":-#",
        ":-*", "^o)", "8-)", "(L)", "(U)", "(D)", "(B)", "(@)", "(&)", "(SN)",
        "(BAH)", "(S)", "(*)", "(#)", "(R)", "({)", "(})", "(K)", "(F)", "(W)",
        "(O)", "(G)", "(^)", "(P)", "(I)", "(C)", "(T)", "(MP)", "(AU)", "(AP)",
        "(CO)", "(MO)", "(~)", "(8)", "(PI)", "(SO)", "(E)", "(Z)", "(X)", "(IP)",
        "(UM)", "(OK)", "(SS)", "(Y)", "(N)", "(YN)", "(H5)", "(TU)", ":-[", "(M)",
        ":)", ":D", ";)", ":O", ":P", ":@", ":S", ":$", ":(", ":|", ":[",
    };

    private static readonly int[] Pics =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60,
        61, 62, 63, 64, 65, 66, 67, 68, 69, 70,
        1, 2, 3, 4, 5, 7, 8, 9, 10, 12, 70,
    };

    private static readonly Dictionary<int, Bitmap?> Cache = new();

    /// <summary>Longest codes first so that ":-)" wins over ":)".</summary>
    private static readonly (string Code, int Pic)[] Ordered = Codes.Select((c, i) => (c, Pics[i])).OrderByDescending(x => x.c.Length).ToArray();

    public static Bitmap? Picture(int n)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(n, out var b)) return b;
            try { b = new Bitmap(AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/smileys/{n:00}.png"))); }
            catch (Exception) { b = null; }
            Cache[n] = b;
            return b;
        }
    }

    /// <summary>Finds the emoticon that starts at <paramref name="index"/>, if any.</summary>
    public static (int Length, int Pic)? At(string text, int index)
    {
        char c = text[index];
        if (c is not (':' or ';' or '(' or '8' or '+' or '<' or '|' or '*' or '^')) return null;
        foreach (var (code, pic) in Ordered)
            if (string.Compare(text, index, code, 0, code.Length, StringComparison.OrdinalIgnoreCase) == 0 && index + code.Length <= text.Length)
                return (code.Length, pic);
        return null;
    }
}
