using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Gta6Countdown;

internal static class WebViewEnvironment
{
    private static readonly object Gate = new();
    private static Task<CoreWebView2Environment>? _environmentTask;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        lock (Gate)
        {
            if (_environmentTask is not null) return _environmentTask;

            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            Directory.CreateDirectory(WidgetPaths.WebViewDataDirectory);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments =
                    "--disable-background-timer-throttling " +
                    "--disable-renderer-backgrounding " +
                    "--disable-backgrounding-occluded-windows"
            };

            _environmentTask = CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: WidgetPaths.WebViewDataDirectory,
                options: options);
            return _environmentTask;
        }
    }

    public static void Configure(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("GTA6_DEVTOOLS") == "1";
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsSwipeNavigationEnabled = false;

        core.SetVirtualHostNameToFolderMapping(
            "gta6.widget",
            WidgetPaths.AssetsDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(
            "gta6.data",
            WidgetPaths.DataDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
    }
}
