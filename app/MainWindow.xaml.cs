using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Gta6Countdown;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4741;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkS = 0x53;
    private const int WmHotkey = 0x0312;

    private readonly DesktopHost _desktopHost = new();
    private Forms.NotifyIcon? _tray;
    private Forms.ContextMenuStrip? _trayMenu;
    private Drawing.Icon? _trayIcon;
    private DispatcherTimer? _wallpaperTimer;
    private DispatcherTimer? _backgroundTimer;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private SettingsWindow? _settingsWindow;
    private WidgetSettings _settings = WidgetSettings.Defaults();
    private bool _renderingBackground;
    private bool _backgroundRefreshPending;
    private string? _updateHudPending;
    private bool _updateHudShown;
    private bool _browserReady;
    private bool _started;
    private bool _refreshing;
    private bool _closing;
    private bool _iconsHidden;
    private bool _hotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        Opacity = 0;
        ShowActivated = false;
        _settings = SettingsService.Load();

        var bounds = Forms.Screen.PrimaryScreen?.Bounds;
        if (bounds is { } b)
        {
            Left = b.Left;
            Top = b.Top;
            Width = b.Width;
            Height = b.Height;
        }

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwnd = hwnd;
        DesktopHost.MakeNonActivating(hwnd);

        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        _hotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, ModControl | ModAlt, VkS);
        WidgetLog.Write($"Global hotkey Ctrl+Alt+S registered={_hotkeyRegistered}.");

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var screen = Forms.Screen.PrimaryScreen?.Bounds;
        WidgetLog.Write(
            $"Window source initialized: hwnd=0x{hwnd.ToInt64():X}, WPF={ActualWidth}x{ActualHeight}, " +
            $"screen={screen?.Width}x{screen?.Height}, system={SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}, " +
            $"dpi={dpi.DpiScaleX:0.##}x{dpi.DpiScaleY:0.##}, lang={_settings.Language}, tray={_settings.ShowTrayIcon}.");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            Directory.CreateDirectory(WidgetPaths.WebViewDataDirectory);

            if (_settings.ShowTrayIcon) SetupTray(showBalloon: true);
            WallpaperService.RefreshIfChanged(force: true);

            if (UpdateService.TryTakePendingVersion(out var pendingVersion))
            {
                _updateHudPending = pendingVersion;
                WidgetLog.Write($"Update notice queued for v{pendingVersion}.");
            }

            StartBackgroundTimer();
            await RenderBackgroundAsync();

            await InitializeBrowserAsync();

            // WPF's layered-window implementation (Opacity < 1) can make cross-process
            // SetParent fail on some Explorer builds, so become opaque just before mounting.
            Opacity = 1;
            _desktopHost.Attach(this);

            Browser.CoreWebView2.Navigate("https://gta6.widget/index.html");
            StartWallpaperWatcher();

            WidgetLog.Write("Widget startup sequence complete.");

            _ = CheckForUpdatesAsync();

            if (System.Windows.Application.Current is App app && app.ConsumePendingShowSettings())
            {
                ShowSettings();
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Startup failed: {ex}");
            ShowFallback(ex.Message);
        }
    }

    private async Task InitializeBrowserAsync()
    {
        var environment = await WebViewEnvironment.GetAsync();
        await Browser.EnsureCoreWebView2Async(environment);

        var core = Browser.CoreWebView2;
        WebViewEnvironment.Configure(core);
        core.NavigationCompleted += OnNavigationCompleted;
        core.ProcessFailed += (_, args) => WidgetLog.Write($"WebView2 process failed: {args.ProcessFailedKind}");
        core.NewWindowRequested += (_, args) => args.Handled = true;

        // Give the page its settings before its first paint, avoiding a language flash.
        var settingsJson = SettingsService.ToJson(_settings);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            $"window.__WIDGET_SETTINGS__ = {settingsJson};");

        Browser.DefaultBackgroundColor = Drawing.Color.FromArgb(255, 10, 6, 16);
        _browserReady = true;
        WidgetLog.Write("WebView2 initialized.");
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        WidgetLog.Write($"Navigation completed: success={e.IsSuccess}, status={e.WebErrorStatus}.");
        if (e.IsSuccess)
        {
            Opacity = 1;
            _ = PushSettingsAsync();
            _ = PushBackgroundAsync();
        }
        else
        {
            ShowFallback($"页面加载失败 / Page failed: {e.WebErrorStatus}");
        }
    }

    public void ShowSettings()
    {
        if (_closing) return;

        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        var window = new SettingsWindow(_settings.Clone());
        window.SettingsChanged += OnSettingsChanged;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, window))
            {
                _settingsWindow = null;
            }
        };

        _settingsWindow = window;
        window.Show();
        window.Activate();
        window.Focus();
        WidgetLog.Write("Settings window opened.");
    }

    private void OnSettingsChanged(object? sender, WidgetSettings settings)
    {
        ApplySettings(settings, persist: true);
    }

    public void ApplySettings(WidgetSettings settings, bool persist)
    {
        var previous = _settings;
        _settings = settings.Sanitized();

        if (persist)
        {
            SettingsService.Save(_settings);
        }

        if (previous.Language != _settings.Language)
        {
            WidgetLog.Write($"Language changed to {_settings.Language}.");
            AppIdentity.UpdateDisplayName(_settings.Language);
        }

        if (previous.ShowTrayIcon != _settings.ShowTrayIcon || previous.Language != _settings.Language)
        {
            if (_settings.ShowTrayIcon)
            {
                SetupTray(showBalloon: false);
            }
            else
            {
                DisposeTray();
            }
        }

        _ = PushSettingsAsync();
        ScheduleBackgroundRender();
    }

    private async Task PushSettingsAsync()
    {
        if (!_browserReady || Browser.CoreWebView2 is null) return;

        try
        {
            var json = SettingsService.ToJson(_settings);
            await Browser.CoreWebView2.ExecuteScriptAsync(
                $"window.applySettings && window.applySettings({json});");
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings push failed: {ex.Message}");
        }
    }

    private void StartBackgroundTimer()
    {
        _backgroundTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(260)
        };
        _backgroundTimer.Tick += async (_, _) =>
        {
            _backgroundTimer?.Stop();
            await RenderBackgroundAsync();
        };
    }

    private void ScheduleBackgroundRender(bool immediate = false)
    {
        if (_backgroundTimer is null) return;

        _backgroundTimer.Stop();
        _backgroundTimer.Interval = TimeSpan.FromMilliseconds(immediate ? 30 : 260);
        _backgroundTimer.Start();
    }

    private async Task RenderBackgroundAsync()
    {
        if (_closing) return;

        if (_renderingBackground)
        {
            _backgroundRefreshPending = true;
            return;
        }

        _renderingBackground = true;
        try
        {
            do
            {
                _backgroundRefreshPending = false;
                var (width, height) = BackgroundRenderer.GetRenderSize(1, 1);
                var changed = await BackgroundRenderer.RenderAsync(
                    _settings.Clone(),
                    WidgetPaths.WallpaperJpg,
                    width,
                    height);
                if (changed)
                {
                    await PushBackgroundAsync();
                }

                if (changed && !_updateHudShown && _updateHudPending is { Length: > 0 } pending)
                {
                    _updateHudShown = true;
                    _updateHudPending = null;
                    UpdateHud.Begin(pending, width, height);
                    WidgetLog.Write($"Update notice shown for v{pending}.");
                }
            }
            while (_backgroundRefreshPending && !_closing);
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Background update failed: {ex}");
        }
        finally
        {
            _renderingBackground = false;
        }
    }

    private async Task PushBackgroundAsync()
    {
        if (!_browserReady || Browser.CoreWebView2 is null) return;

        var url = "https://gta6.data/background.jpg?v=" + DateTimeOffset.Now.ToUnixTimeMilliseconds();
        await PushBackgroundUrl($"window.reloadBackground && window.reloadBackground({JsonSerializer.Serialize(url)});");
    }

    /// <summary>Used by <see cref="UpdateHud"/> to swap the notice frames.</summary>
    public async Task PushBackgroundUrl(string script)
    {
        if (!_browserReady || Browser.CoreWebView2 is null) return;

        try
        {
            await Browser.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Background push failed: {ex.Message}");
        }
    }

    private void StartWallpaperWatcher()
    {
        _wallpaperTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(18)
        };
        _wallpaperTimer.Tick += OnWallpaperTimerTick;
        _wallpaperTimer.Start();
    }

    private async void OnWallpaperTimerTick(object? sender, EventArgs e)
    {
        if (_closing || _refreshing) return;
        _refreshing = true;

        try
        {
            if (!_desktopHost.IsAttached)
            {
                WidgetLog.Write("Desktop host lost; reattaching.");
                _desktopHost.Attach(this);
            }

            if (WallpaperService.RefreshIfChanged())
            {
                await RenderBackgroundAsync();
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Wallpaper watcher failed: {ex}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await UpdateService.CheckAsync();
            if (result.State == "available" && !_updateHudShown && _updateHudPending is null)
            {
                _updateHudPending = result.LatestVersion;
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Startup update check failed: {ex.Message}");
        }
    }

    private string L(string zh, string en) => _settings.Language == "en" ? en : zh;

    private void SetupTray(bool showBalloon)
    {
        DisposeTray();

        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = Drawing.Color.FromArgb(20, 12, 28),
            ForeColor = Drawing.Color.FromArgb(255, 247, 251),
            Renderer = new DarkMenuRenderer(),
            Padding = new Forms.Padding(4)
        };

        var header = new Forms.ToolStripMenuItem("GTA VI · 2026.11.18  19:00")
        {
            Enabled = false,
            ForeColor = Drawing.Color.FromArgb(255, 112, 184)
        };
        menu.Items.Add(header);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var settingsItem = new Forms.ToolStripMenuItem(L("效果与系统设置…", "Effects & system settings…"));
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var refresh = new Forms.ToolStripMenuItem(L("刷新壁纸与计时", "Refresh wallpaper & clock"));
        refresh.Click += async (_, _) => await RefreshWidgetAsync();
        menu.Items.Add(refresh);

        var remount = new Forms.ToolStripMenuItem(L("重新贴到桌面", "Reattach to desktop"));
        remount.Click += (_, _) =>
        {
            var ok = _desktopHost.Reattach(this);
            WidgetLog.Write($"Manual reattach: {(ok ? "WorkerW" : "fallback window")}");
        };
        menu.Items.Add(remount);

        var hideIcons = new Forms.ToolStripMenuItem(L("隐藏桌面图标（只留倒计时）", "Hide desktop icons"))
        {
            CheckOnClick = true,
            Checked = _iconsHidden
        };
        hideIcons.Click += (_, _) =>
        {
            if (DesktopHost.SetDesktopIconsVisible(!hideIcons.Checked))
            {
                _iconsHidden = hideIcons.Checked;
            }
            else
            {
                hideIcons.Checked = false;
                _iconsHidden = false;
            }
        };
        menu.Items.Add(hideIcons);

        var autoStart = new Forms.ToolStripMenuItem(L("开机自动启动", "Start with Windows"))
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled()
        };
        autoStart.Click += (_, _) =>
        {
            try
            {
                StartupManager.SetEnabled(autoStart.Checked);
                WidgetLog.Write($"Autostart set to {autoStart.Checked}.");
            }
            catch (Exception ex)
            {
                WidgetLog.Write($"Autostart change failed: {ex}");
                autoStart.Checked = StartupManager.IsEnabled();
            }
        };
        menu.Items.Add(autoStart);

        var openData = new Forms.ToolStripMenuItem(L("打开数据文件夹", "Open data folder"));
        openData.Click += (_, _) => OpenDataFolder();
        menu.Items.Add(openData);

        menu.Items.Add(new Forms.ToolStripSeparator());
        var exit = new Forms.ToolStripMenuItem(L("退出", "Exit"));
        exit.Click += (_, _) => ExitWidget();
        menu.Items.Add(exit);

        menu.Opening += (_, _) =>
        {
            autoStart.Checked = StartupManager.IsEnabled();
            hideIcons.Checked = _iconsHidden;
        };

        _trayMenu = menu;
        var iconPath = Path.Combine(WidgetPaths.AssetsDirectory, "app.ico");
        _trayIcon = File.Exists(iconPath)
            ? new Drawing.Icon(iconPath)
            : (Drawing.Icon)Drawing.SystemIcons.Application.Clone();

        _tray = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = L("GTA VI 倒计时 · 2026.11.18 19:00", "GTA VI countdown · 2026.11.18 19:00"),
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _tray.BalloonTipTitle = L("GTA VI 发售倒计时", "GTA VI release countdown");
        _tray.BalloonTipText = L(
            "桌面倒计时已就位。双击托盘图标可打开效果设置。",
            "Desktop countdown is ready. Double-click the tray icon to open settings.");
        _tray.BalloonTipIcon = Forms.ToolTipIcon.None;

        if (showBalloon)
        {
            _tray.ShowBalloonTip(2200);
        }

        WidgetLog.Write($"Tray icon created (language={_settings.Language}).");
    }

    private void DisposeTray()
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        _trayMenu?.Dispose();
        _trayMenu = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private static void OpenDataFolder()
    {
        Directory.CreateDirectory(WidgetPaths.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{WidgetPaths.DataDirectory}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task RefreshWidgetAsync()
    {
        if (_refreshing || _closing) return;
        _refreshing = true;

        try
        {
            WallpaperService.RefreshIfChanged(force: true);
            _desktopHost.Attach(this);
            await RenderBackgroundAsync();

            if (_browserReady)
            {
                await PushSettingsAsync();
                await PushBackgroundAsync();
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Manual refresh failed: {ex}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ShowFallback(string detail)
    {
        try
        {
            Browser.Visibility = Visibility.Collapsed;
            FallbackDetail.Text = $"{detail}\n\n日志 / Log: {WidgetPaths.LogFile}";
            FallbackPanel.Visibility = Visibility.Visible;
            Opacity = 1;
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Could not show fallback UI: {ex}");
        }
    }

    private void ExitWidget()
    {
        if (_closing) return;
        _closing = true;
        _wallpaperTimer?.Stop();
        DisposeTray();
        _settingsWindow?.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ShowSettings();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closing = true;
        if (_iconsHidden)
        {
            DesktopHost.SetDesktopIconsVisible(true);
            _iconsHidden = false;
        }

        if (_hotkeyRegistered)
        {
            try
            {
                UnregisterHotKey(_hwnd, HotkeyId);
            }
            catch (Exception ex)
            {
                WidgetLog.Write($"Hotkey cleanup failed: {ex.Message}");
            }

            _hotkeyRegistered = false;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;

        if (_settingsWindow is not null)
        {
            _settingsWindow.SettingsChanged -= OnSettingsChanged;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        _wallpaperTimer?.Stop();
        _backgroundTimer?.Stop();
        _backgroundTimer = null;
        DisposeTray();

        try
        {
            Browser.Dispose();
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Browser disposal failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private sealed class DarkMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(20, 12, 28));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(64, 255, 80, 170), 1);
            var r = new Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, r);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
            {
                return;
            }

            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(58, 255, 47, 150));
            e.Graphics.FillRectangle(brush, new Drawing.Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2));
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled
                ? Drawing.Color.FromArgb(255, 247, 251)
                : Drawing.Color.FromArgb(170, 255, 112, 184);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(40, 255, 255, 255), 1);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class DarkMenuColors : Forms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(20, 12, 28);
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(58, 255, 47, 150);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(58, 255, 47, 150);
        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(20, 12, 28);
        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(20, 12, 28);
        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(20, 12, 28);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(40, 255, 255, 255);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(40, 255, 255, 255);
    }
}
