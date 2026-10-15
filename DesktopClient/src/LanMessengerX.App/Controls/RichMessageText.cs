using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LanMessengerX.App.Services;

namespace LanMessengerX.App.Controls;

/// <summary>Message text with clickable links (web addresses, optionally network paths) and the original smileys.</summary>
public sealed class RichMessageText : SelectableTextBlock
{
    public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<RichMessageText, string?>(nameof(Message));
    public static readonly StyledProperty<IBrush?> LinkBrushProperty = AvaloniaProperty.Register<RichMessageText, IBrush?>(nameof(LinkBrush));

    private static readonly Regex Links = new(@"((?:https?|ftp)://[^\s<>""]+|www\.[^\s<>""]+|\\\\[^\s<>""\\/:*?|]+(?:\\[^\s<>""\\/:*?|]+)*\\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public IBrush? LinkBrush { get => GetValue(LinkBrushProperty); set => SetValue(LinkBrushProperty, value); }

    static RichMessageText()
    {
        MessageProperty.Changed.AddClassHandler<RichMessageText>((t, _) => t.Rebuild());
        LinkBrushProperty.Changed.AddClassHandler<RichMessageText>((t, _) => t.Rebuild());
    }

    public RichMessageText() => TextWrapping = TextWrapping.Wrap;

    private void Rebuild()
    {
        var text = Message ?? "";
        var s = AppState.Current?.Settings;
        bool links = s?.AllowLinks ?? true, paths = s?.PathToLink ?? true, smileys = s?.ShowEmoticons ?? true;
        Inlines?.Clear();
        Text = null;
        if (!links && !smileys)
        {
            Text = text;
            return;
        }
        var inlines = Inlines ??= new InlineCollection();
        int pos = 0;
        var plain = new System.Text.StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            inlines.Add(new Run(plain.ToString()));
            plain.Clear();
        }
        MatchCollection matches = links ? Links.Matches(text) : Regex.Matches("", "$^");
        int mi = 0;
        while (pos < text.Length)
        {
            if (mi < matches.Count && matches[mi].Index == pos)
            {
                var m = matches[mi++];
                var target = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
                bool isPath = target.StartsWith("\\\\");
                if (isPath && !paths) { plain.Append(m.Value); pos += m.Length; continue; }
                Flush();
                inlines.Add(MakeLink(target));
                if (target.Length < m.Length) plain.Append(m.Value, target.Length, m.Length - target.Length);
                pos += m.Length;
                continue;
            }
            if (smileys && Emoticons.At(text, pos) is { } e && Emoticons.Picture(e.Pic) is { } bmp && !InsideLink(matches, mi, pos))
            {
                Flush();
                inlines.Add(new InlineUIContainer(new Image { Source = bmp, Width = 19, Height = 19, Margin = new Thickness(1, 0), VerticalAlignment = VerticalAlignment.Center }));
                pos += e.Length;
                continue;
            }
            plain.Append(text[pos++]);
        }
        Flush();
    }

    private static bool InsideLink(MatchCollection matches, int next, int pos) => false;

    private Inline MakeLink(string target)
    {
        var label = new TextBlock
        {
            Text = target,
            TextDecorations = Avalonia.Media.TextDecorations.Underline,
            Foreground = LinkBrush ?? Brushes.DodgerBlue,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        label.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(label).Properties.IsLeftButtonPressed) return;
            Shell.Open(target.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + target : target);
            e.Handled = true;
        };
        return new InlineUIContainer(label);
    }
}
