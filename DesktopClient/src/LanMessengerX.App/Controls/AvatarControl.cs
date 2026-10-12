using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LanMessengerX.App.ViewModels;

namespace LanMessengerX.App.Controls;

/// <summary>Round avatar: the picture if there is one, otherwise the initial on a colour derived from the name; optional presence dot.</summary>
public class AvatarControl : Control
{
    public static readonly StyledProperty<string?> DisplayNameProperty = AvaloniaProperty.Register<AvatarControl, string?>(nameof(DisplayName));
    public static readonly StyledProperty<Bitmap?> ImageProperty = AvaloniaProperty.Register<AvatarControl, Bitmap?>(nameof(Image));
    public static readonly StyledProperty<string?> StatusProperty = AvaloniaProperty.Register<AvatarControl, string?>(nameof(Status));

    static AvatarControl()
    {
        AffectsRender<AvatarControl>(DisplayNameProperty, ImageProperty, StatusProperty);
    }

    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public Bitmap? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    private static readonly Color[] Palette =
    {
        Color.Parse("#00796B"), Color.Parse("#1565C0"), Color.Parse("#6A1B9A"), Color.Parse("#C62828"),
        Color.Parse("#EF6C00"), Color.Parse("#2E7D32"), Color.Parse("#AD1457"), Color.Parse("#455A64"),
    };

    public override void Render(DrawingContext ctx)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;
        var rect = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        var circle = new EllipseGeometry(rect);
        var img = Image;
        if (img != null)
        {
            using (ctx.PushGeometryClip(circle))
            {
                double side = Math.Min(img.Size.Width, img.Size.Height);
                var src = new Rect((img.Size.Width - side) / 2, (img.Size.Height - side) / 2, side, side);
                ctx.DrawImage(img, src, rect);
            }
        }
        else
        {
            var name = DisplayName ?? "?";
            int hash = 17;
            foreach (var ch in name) hash = hash * 31 + ch;
            var color = Palette[(hash & 0x7FFFFFFF) % Palette.Length];
            ctx.DrawGeometry(new SolidColorBrush(color), null, circle);
            var initial = name.Trim().Length > 0 ? char.ToUpperInvariant(name.Trim()[0]).ToString() : "?";
            var text = new FormattedText(initial, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), size * 0.44, Brushes.White);
            ctx.DrawText(text, new Point(rect.X + (size - text.Width) / 2, rect.Y + (size - text.Height) / 2));
        }
        var status = Status;
        if (status != null)
        {
            double dot = Math.Max(8, size * 0.28);
            var c = new Point(rect.Right - dot / 2, rect.Bottom - dot / 2);
            IBrush ring = this.TryFindResource("SolidBackgroundFillColorBase", ActualThemeVariant, out var r) && r is Color rc
                ? new SolidColorBrush(rc) : Brushes.White;
            ctx.DrawEllipse(ring, null, c, dot / 2 + 1.5, dot / 2 + 1.5);
            ctx.DrawEllipse(StatusText.Presence(status), null, c, dot / 2, dot / 2);
        }
    }
}
