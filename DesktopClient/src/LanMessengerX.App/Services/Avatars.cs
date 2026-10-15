using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LanMessengerX.App.Services;

/// <summary>The 20 stock pictures of the original LAN Messenger, and turning any picture into the profile picture.</summary>
public static class Avatars
{
    public const int StockCount = 20;
    private static readonly Bitmap?[] Cache = new Bitmap?[StockCount];

    public static Bitmap? Stock(int index)
    {
        if (index < 0 || index >= StockCount) return null;
        try
        {
            return Cache[index] ??= new Bitmap(AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/avatars/{index}.png")));
        }
        catch (Exception e)
        {
            FileLog.Write($"stock picture {index}: {e.Message}");
            return null;
        }
    }

    /// <summary>First start: like the original, pick a random stock picture so that nobody shows up as a blank circle.</summary>
    public static void EnsureDefault(AppState state)
    {
        var s = state.Settings;
        if (s.StockAvatar != null && File.Exists(state.AvatarFile)) return;
        if (s.StockAvatar == -1 && File.Exists(state.AvatarFile)) return;
        UseStock(state, Random.Shared.Next(StockCount));
    }

    public static void UseStock(AppState state, int index)
    {
        try
        {
            using var src = AssetLoader.Open(new Uri($"avares://LanMessengerX/Assets/avatars/{index}.png"));
            using (var dst = File.Create(state.AvatarFile)) src.CopyTo(dst);
            state.Settings.StockAvatar = index;
            state.Settings.Save();
            state.SetAvatar(state.AvatarFile);
        }
        catch (Exception e) { FileLog.Write($"could not use stock picture {index}: {e.Message}"); }
    }

    /// <summary>Crops the picture square, scales it to 128x128 and stores it as the profile picture. Returns an error text or null.</summary>
    public static string? UseFile(AppState state, string path)
    {
        try
        {
            using var src = new Bitmap(path);
            var target = new RenderTargetBitmap(new PixelSize(128, 128), new Vector(96, 96));
            using (var ctx = target.CreateDrawingContext())
            {
                double side = Math.Min(src.Size.Width, src.Size.Height);
                var from = new Rect((src.Size.Width - side) / 2, (src.Size.Height - side) / 2, side, side);
                ctx.DrawImage(src, from, new Rect(0, 0, 128, 128));
            }
            target.Save(state.AvatarFile);
            state.Settings.StockAvatar = -1;
            state.Settings.Save();
            state.SetAvatar(state.AvatarFile);
            return null;
        }
        catch (Exception e)
        {
            FileLog.Write($"could not use picture {path}: {e}");
            return e.Message;
        }
    }
}
