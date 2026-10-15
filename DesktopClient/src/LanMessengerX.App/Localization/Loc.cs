using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia.Platform;

namespace LanMessengerX.App.Localization;

/// <summary>
/// UI texts. The English text is the key (like Qt's tr()); the translations live in Assets/lang/&lt;code&gt;.json, generated
/// by tools/i18n/build_lang.py from the translation files of the original LAN Messenger plus our own additions.
/// Anything without a translation is shown in English.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    /// <summary>Languages offered in the settings: the translations of the original LAN Messenger, plus Hungarian.</summary>
    public static readonly (string Code, string Name)[] Languages =
    {
        ("ar_SA", "العربية"), ("bg_BG", "Български"), ("de_DE", "Deutsch"), ("en_US", "English"), ("es_ES", "Español"),
        ("fr_FR", "Français"), ("hu_HU", "Magyar"), ("it_IT", "Italiano"), ("ja_JP", "日本語"), ("ko_KR", "한국어"),
        ("ml_IN", "മലയാളം"), ("pl_PL", "Polski"), ("pt_BR", "Português (Brasil)"), ("ro_RO", "Română"), ("ru_RU", "Русский"),
        ("sk_SK", "Slovenčina"), ("sl_SI", "Slovenščina"), ("sv_SE", "Svenska"), ("tr_TR", "Türkçe"),
    };

    private static Dictionary<string, string> _map = new();

    public static string Code { get; private set; } = "en_US";

    /// <summary>Changes whenever the language changes; bindings listen to it.</summary>
    public int Revision { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string T(string english) => _map.TryGetValue(english, out var t) && t.Length > 0 ? t : english;

    public static string F(string english, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(english), args);

    public static bool IsRightToLeft => Code == "ar_SA";

    /// <summary>"system" or a code from <see cref="Languages"/>.</summary>
    public static void Use(string setting)
    {
        var code = setting == "system" || string.IsNullOrEmpty(setting) ? FromSystem() : setting;
        Dictionary<string, string> map = new();
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/lang/{code}.json"));
            map = JsonSerializer.Deserialize<Dictionary<string, string>>(s) ?? new();
        }
        catch (Exception) { /* no such language file: English */ }
        _map = map;
        Code = code;
        try { CultureInfo.CurrentUICulture = new CultureInfo(code.Replace('_', '-')); } catch (Exception) { }
        Instance.Revision++;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Revision)));
    }

    public static string FromSystem()
    {
        var ui = CultureInfo.InstalledUICulture.Name.Replace('-', '_'); // e.g. hu_HU
        var exact = Languages.FirstOrDefault(l => string.Equals(l.Code, ui, StringComparison.OrdinalIgnoreCase));
        if (exact.Code != null) return exact.Code;
        var lang = ui.Split('_')[0];
        var byLang = Languages.FirstOrDefault(l => l.Code.StartsWith(lang + "_", StringComparison.OrdinalIgnoreCase));
        return byLang.Code ?? "en_US";
    }
}
