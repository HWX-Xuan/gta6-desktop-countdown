using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Gta6Countdown;

internal static class WallpaperService
{
    private const uint SPI_GETDESKWALLPAPER = 0x0073;
    private const int MaxWidth = 2560;

    private static string _lastKey = string.Empty;
    private static bool _busy;

    public static bool RefreshIfChanged(bool force = false)
    {
        if (_busy) return false;
        _busy = true;

        try
        {
            var path = GetWallpaperPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                WidgetLog.Write("Wallpaper refresh skipped: no wallpaper file found.");
                return false;
            }

            WidgetPaths.CurrentWallpaperPath = path;
            var info = new FileInfo(path);
            var key = $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
            if (!force && key == _lastKey && File.Exists(WidgetPaths.WallpaperJpg))
            {
                return false;
            }

            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            ConvertToDisplayWallpaper(path);
            _lastKey = key;
            WidgetLog.Write($"Wallpaper converted: {path} ({info.Length} bytes) -> {WidgetPaths.WallpaperJpg}");
            return true;
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Wallpaper refresh failed: {ex}");
            return false;
        }
        finally
        {
            _busy = false;
        }
    }

    private static void ConvertToDisplayWallpaper(string sourcePath)
    {
        using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidOperationException("Wallpaper has no image frames.");

        BitmapSource source = decoder.Frames[0];
        if (source.PixelWidth > MaxWidth)
        {
            var scale = MaxWidth / (double)source.PixelWidth;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = 93 };
        encoder.Frames.Add(BitmapFrame.Create(source));

        var tempPath = WidgetPaths.WallpaperJpg + ".tmp";
        using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(output);
        }

        File.Move(tempPath, WidgetPaths.WallpaperJpg, true);
    }

    private static string GetWallpaperPath()
    {
        var buffer = new StringBuilder(2048);
        if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)buffer.Capacity, buffer, 0) &&
            buffer.Length > 0)
        {
            return buffer.ToString();
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("WallPaper") as string ?? string.Empty;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action, uint parameter, StringBuilder value, uint updateIni);
}
