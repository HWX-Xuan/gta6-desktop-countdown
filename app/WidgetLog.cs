using System.IO;

namespace Gta6Countdown;

internal static class WidgetLog
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            lock (Gate)
            {
                if (File.Exists(WidgetPaths.LogFile) && new FileInfo(WidgetPaths.LogFile).Length > 512 * 1024)
                {
                    File.Delete(WidgetPaths.LogFile);
                }

                File.AppendAllText(
                    WidgetPaths.LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging should never take down the widget.
        }
    }
}
