using System.IO;
using System.Threading;
using System.Windows;

namespace Gta6Countdown;

public partial class App : System.Windows.Application
{
    private const string ShowSettingsEventName = @"Local\GTA6CountdownShowSettings";

    private FileStream? _instanceLock;
    private EventWaitHandle? _showSettingsEvent;
    private Thread? _showSettingsListener;
    private bool _pendingShowSettings;

    protected override void OnStartup(StartupEventArgs e)
    {
        WidgetLog.Write("App startup.");

        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            _instanceLock = new FileStream(
                Path.Combine(WidgetPaths.DataDirectory, "instance.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            WidgetLog.Write("Another countdown instance is already running; asking it to show settings.");
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowSettingsEventName);
                showEvent.Set();
            }
            catch
            {
                // The existing instance may still be starting up; nothing else to do.
            }

            Environment.Exit(0);
            return;
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Instance lock failed, continuing anyway: {ex.Message}");
        }

        var startupSettings = SettingsService.Load();
        AppIdentity.Apply(startupSettings.Language);

        try
        {
            _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
            _showSettingsListener = new Thread(ShowSettingsListenerLoop)
            {
                IsBackground = true,
                Name = "GTA6Countdown settings listener"
            };
            _showSettingsListener.Start();
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Could not create settings activation event: {ex.Message}");
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            WidgetLog.Write($"Unhandled UI exception: {args.Exception}");
            args.Handled = true;
        };

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void ShowSettingsListenerLoop()
    {
        var handle = _showSettingsEvent;
        if (handle is null) return;

        while (true)
        {
            try
            {
                handle.WaitOne();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                WidgetLog.Write($"Settings listener stopped: {ex.Message}");
                return;
            }

            RequestShowSettings();
        }
    }

    public void RequestShowSettings()
    {
        _pendingShowSettings = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (MainWindow is not MainWindow window) return;
            _pendingShowSettings = false;
            window.ShowSettings();
        }));
    }

    public bool ConsumePendingShowSettings()
    {
        var pending = _pendingShowSettings;
        _pendingShowSettings = false;
        return pending;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showSettingsEvent?.Dispose();
        _showSettingsEvent = null;
        _instanceLock?.Dispose();
        _instanceLock = null;
        base.OnExit(e);
    }
}
