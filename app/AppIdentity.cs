using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Gta6Countdown;

/// <summary>
/// When hosted by dotnet.exe, Windows otherwise labels the taskbar button
/// ".NET Host" and uses the generic dotnet icon. An explicit AppUserModelID
/// plus registered display name/icon gives the process our own identity.
/// </summary>
internal static class AppIdentity
{
    public const string AppUserModelId = "GTA6.Countdown.Desktop.v1";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appUserModelId);

    public static void Apply(string language = "zh")
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"AUMID apply failed: {ex.Message}");
        }

        UpdateDisplayName(language);
    }

    public static void UpdateDisplayName(string language)
    {
        try
        {
            var displayName = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? "GTA VI Countdown"
                : "GTA VI 倒计时";

            using var key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\AppUserModelId\{AppUserModelId}");
            if (key is null) return;

            key.SetValue("DisplayName", displayName, RegistryValueKind.String);
            var icon = Path.Combine(WidgetPaths.AssetsDirectory, "app.ico");
            if (File.Exists(icon))
            {
                key.SetValue("IconUri", icon, RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"AUMID registration failed: {ex.Message}");
        }
    }
}
