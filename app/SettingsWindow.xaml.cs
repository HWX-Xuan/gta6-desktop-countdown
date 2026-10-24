using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Drawing = System.Drawing;

namespace Gta6Countdown;

public partial class SettingsWindow : Window
{
    private WidgetSettings _settings;
    private bool _closed;
    private bool _browserReady;
    private UpdateCheckResult _update = UpdateCheckResult.Unknown;
    private bool _updateCheckRunning;

    public event EventHandler<WidgetSettings>? SettingsChanged;

    public SettingsWindow(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings.Sanitized();
        ApplyTitleLanguage();
        FitToScreen();

        Loaded += OnLoaded;
        Closed += OnClosed;

        var iconPath = Path.Combine(WidgetPaths.AssetsDirectory, "app-icon.png");
        if (File.Exists(iconPath))
        {
            try
            {
                var icon = new BitmapImage(new Uri(iconPath));
                AppIcon.Source = icon;
                Icon = icon;
            }
            catch (Exception ex)
            {
                WidgetLog.Write($"Settings icon load failed: {ex.Message}");
            }
        }
    }

    private void FitToScreen()
    {
        // Scaling 150% on a 2K laptop leaves only ~1070x710 DIPs; shrink instead
        // of running off the screen edge.
        try
        {
            var area = SystemParameters.WorkArea;
            var width = Math.Min(Width, Math.Max(MinWidth, area.Width - 40));
            var height = Math.Min(Height, Math.Max(MinHeight, area.Height - 40));
            Width = width;
            Height = height;
            WidgetLog.Write($"Settings window fitted to {width:0}x{height:0} DIPs (work area {area.Width:0}x{area.Height:0}).");
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings fit failed: {ex.Message}");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WidgetLog.Write("Settings window loaded; initializing WebView2.");
        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            await Browser.EnsureCoreWebView2Async(environment);

            var core = Browser.CoreWebView2;
            WebViewEnvironment.Configure(core);
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += OnNavigationCompleted;
            core.ProcessFailed += (_, args) =>
                WidgetLog.Write($"Settings WebView2 process failed: {args.ProcessFailedKind}");
            core.NewWindowRequested += (_, args) => args.Handled = true;

            // On scaled displays the whole page renders 1.5x or 2x larger; zoom out so
            // the same amount of content stays visible.
            try
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                core.Settings.IsZoomControlEnabled = false;
                var zoom = dpi.DpiScaleX > 1.01 ? 1.0 / dpi.DpiScaleX : 1.0;
                await core.ExecuteScriptAsync($"document.documentElement.style.zoom = '{zoom:0.###}';");
                WidgetLog.Write($"Settings page zoom={zoom:0.###} (dpi={dpi.DpiScaleX:0.##}).");
            }
            catch (Exception zoomError)
            {
                WidgetLog.Write($"Settings zoom failed: {zoomError.Message}");
            }

            Browser.DefaultBackgroundColor = Drawing.Color.FromArgb(255, 11, 7, 16);
            _browserReady = true;
            core.Navigate("https://gta6.widget/settings.html");
            WidgetLog.Write("Settings WebView2 initialized; navigating to settings page.");
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings WebView2 initialization failed: {ex}");
            LoadingText.Text = $"加载失败 / Failed to load\n{ex.Message}";
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            WidgetLog.Write("Settings page navigation completed.");
        }
        else
        {
            LoadingText.Text = $"加载失败 / Failed to load\n{e.WebErrorStatus}";
            WidgetLog.Write($"Settings page navigation failed: {e.WebErrorStatus}.");
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = JsonSerializer.Deserialize<SettingsMessage>(
                e.WebMessageAsJson,
                SettingsService.JsonOptions);

            if (message is null) return;

            switch (message.Type)
            {
                case "ready":
                    WidgetLog.Write("Settings page ready; sending current settings.");
                    SendInit();
                    break;
                case "save":
                    ApplyFromUi(message.Settings, message.AutoStart);
                    break;
                case "close":
                    Close();
                    break;
                case "openDataFolder":
                    OpenDataFolder();
                    break;
                case "checkUpdate":
                    _ = RunUpdateCheckAsync();
                    break;
                case "openRelease":
                    OpenReleasePage();
                    break;
                case "testUpdateNotice":
                    QueueUpdateNoticeTest();
                    break;
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings message failed: {ex}");
        }
    }

    private void SendInit()
    {
        if (!_browserReady || Browser.CoreWebView2 is null) return;

        var payload = new
        {
            type = "init",
            settings = _settings,
            autoStart = StartupManager.IsEnabled(),
            version = UpdateService.CurrentVersion,
            update = _update,
            updateKnown = UpdateService.CachedResultKnown
        };
        Browser.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(payload, SettingsService.JsonOptions));
    }

    private async Task RunUpdateCheckAsync()
    {
        if (_updateCheckRunning) return;
        _updateCheckRunning = true;

        try
        {
            _update = await UpdateService.CheckAsync(force: true);
            SendUpdateState();
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Manual update check failed: {ex}");
        }
        finally
        {
            _updateCheckRunning = false;
        }
    }

    private void SendUpdateState()
    {
        if (!_browserReady || Browser.CoreWebView2 is null) return;

        var payload = new
        {
            type = "updateState",
            version = UpdateService.CurrentVersion,
            update = _update,
            updateKnown = UpdateService.CachedResultKnown
        };
        Browser.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(payload, SettingsService.JsonOptions));
    }

    private static void OpenReleasePage()
    {
        UpdateService.OpenReleasePage();
    }

    private void QueueUpdateNoticeTest()
    {
        if (!UpdateService.CurrentVersion.EndsWith(".0", StringComparison.Ordinal))
        {
            WidgetLog.Write("Update notice preview is only available in development builds.");
            return;
        }

        try
        {
            var current = UpdateService.CurrentVersion;
            var parts = current.Split('.');
            var preview = $"{parts[0]}.{parts[1]}.{int.Parse(parts[2]) + 1}";
            File.WriteAllText(
                Path.Combine(WidgetPaths.DataDirectory, "pending-update-version.txt"),
                preview);
            WidgetLog.Write($"Update notice preview queued: v{preview}. Restart the widget to see it.");
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Queueing update preview failed: {ex.Message}");
        }
    }

    private void ApplyFromUi(WidgetSettings? incoming, bool requestedAutoStart)
    {
        if (incoming is null) return;

        var settings = incoming.Sanitized();
        string? autoStartError = null;
        var currentAutoStart = StartupManager.IsEnabled();

        if (requestedAutoStart != currentAutoStart)
        {
            try
            {
                StartupManager.SetEnabled(requestedAutoStart);
                if (StartupManager.IsEnabled() != requestedAutoStart)
                {
                    autoStartError = "The startup registry key did not update.";
                }
            }
            catch (Exception ex)
            {
                autoStartError = ex.Message;
                WidgetLog.Write($"Autostart toggle failed: {ex}");
            }
        }

        var actualAutoStart = StartupManager.IsEnabled();
        _settings = settings;
        ApplyTitleLanguage();

        SettingsChanged?.Invoke(this, _settings.Clone());
        WidgetLog.Write(
            $"Settings saved: blur={_settings.Blur:0.#}, brightness={_settings.Brightness:0.##}, " +
            $"glow={_settings.Glow:0.##}, lang={_settings.Language}, tray={_settings.ShowTrayIcon}, " +
            $"autostart={actualAutoStart}.");

        if (!_browserReady || Browser.CoreWebView2 is null) return;

        var response = new
        {
            type = "saved",
            ok = autoStartError is null,
            settings = _settings,
            autoStart = actualAutoStart,
            autoStartError
        };
        Browser.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(response, SettingsService.JsonOptions));
    }

    private void ApplyTitleLanguage()
    {
        var isEnglish = _settings.Language == "en";
        Title = isEnglish ? "GTA VI · Settings" : "GTA VI · 效果设置";
        TitleText.Text = Title;
        SubtitleText.Text = isEnglish ? "EFFECTS & SYSTEM" : "效果与系统";
        MinimizeButton.ToolTip = isEnglish ? "Minimize" : "最小化";
        CloseButton.ToolTip = isEnglish ? "Close" : "关闭";
    }

    private static void OpenDataFolder()
    {
        Directory.CreateDirectory(WidgetPaths.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{WidgetPaths.DataDirectory}\"")
        {
            UseShellExecute = true
        });
    }

    private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try
        {
            DragMove();
        }
        catch
        {
            // Rapid click/release can invalidate the drag; safe to ignore.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_closed) return;
        _closed = true;
        WidgetLog.Write("Settings window closed.");

        try
        {
            if (_browserReady && Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                Browser.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }

            Browser.Dispose();
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings window dispose failed: {ex.Message}");
        }
    }

    private sealed class SettingsMessage
    {
        public string Type { get; set; } = string.Empty;
        public WidgetSettings? Settings { get; set; }
        public bool AutoStart { get; set; }
    }
}
