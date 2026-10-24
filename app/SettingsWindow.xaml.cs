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

    public event EventHandler<WidgetSettings>? SettingsChanged;

    public SettingsWindow(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings.Sanitized();
        ApplyTitleLanguage();

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
            autoStart = StartupManager.IsEnabled()
        };
        Browser.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(payload, SettingsService.JsonOptions));
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
