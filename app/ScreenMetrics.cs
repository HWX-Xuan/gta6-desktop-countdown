using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Gta6Countdown;

internal static class ScreenMetrics
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    public static int PrimaryWidth => Forms.Screen.PrimaryScreen?.Bounds.Width ?? GetSystemMetrics(SmCxScreen);

    public static int PrimaryHeight => Forms.Screen.PrimaryScreen?.Bounds.Height ?? GetSystemMetrics(SmCyScreen);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
