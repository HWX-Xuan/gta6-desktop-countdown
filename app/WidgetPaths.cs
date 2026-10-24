using System.IO;

namespace Gta6Countdown;

internal static class WidgetPaths
{
    public static string AssetsDirectory => Path.Combine(AppContext.BaseDirectory, "assets");

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GTA6Countdown");

    public static string WebViewDataDirectory => Path.Combine(DataDirectory, "WebView2");

    public static string WallpaperJpg => Path.Combine(DataDirectory, "wallpaper.jpg");

    public static string LogFile => Path.Combine(DataDirectory, "widget.log");

    public static string CurrentWallpaperPath { get; set; } = string.Empty;
}
