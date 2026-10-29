using System.Threading;
using System.Windows;
using Spotlight.Services;

namespace Spotlight;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Spotlight.Launcher.SingleInstance";
    private const string OpenEventName = "Spotlight.Launcher.Open";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private MainWindow? _window;
    private TrayIconService? _tray;
    private EventWaitHandle? _openEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            SignalRunningInstance();
            Shutdown();
            return;
        }

        _openEvent = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
        StartOpenListener();

        DispatcherUnhandledException += OnUnhandledException;

        _window = new MainWindow();
        MainWindow = _window;

        var settings = SettingsStore.Load();
        _window.SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey);

        // Create the window handle now (without showing the window) so the
        // global hotkey is registered immediately at startup.
        new System.Windows.Interop.WindowInteropHelper(_window).EnsureHandle();

        _tray = new TrayIconService(
            onOpen: () => _window.ShowAndFocus(),
            onSettings: OpenSettings,
            onExit: Shutdown);
        _tray.Show();
        _tray.SetHotkeyText(HotkeyFormatter.Format(settings.HotkeyModifiers, settings.HotkeyKey));
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(
            _window!.HotkeyModifiers,
            _window.HotkeyKey,
            (modifiers, key) =>
            {
                SettingsStore.Save(new AppSettings
                {
                    HotkeyModifiers = modifiers,
                    HotkeyKey = key,
                });

                bool applied = _window.UpdateHotkey(modifiers, key);
                if (applied)
                {
                    _tray!.SetHotkeyText(HotkeyFormatter.Format(modifiers, key));
                }
                return applied;
            });

        dialog.ShowDialog();
    }

    private void SignalRunningInstance()
    {
        try
        {
            using var open = EventWaitHandle.OpenExisting(OpenEventName);
            open.Set();
        }
        catch
        {
        }
    }

    private void StartOpenListener()
    {
        var handle = _openEvent!;
        Task.Run(() =>
        {
            while (true)
            {
                try
                {
                    if (!handle.WaitOne()) break;
                }
                catch
                {
                    break;
                }
                try
                {
                    Dispatcher.Invoke(() => _window?.ShowAndFocus());
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
        });
    }

    private static void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        System.Windows.MessageBox.Show(
            e.Exception.Message,
            "Spotlight error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogError(Exception ex)
    {
        try
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spotlight-error.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _window?.Cleanup();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
            }
        }
        _mutex?.Dispose();
        _openEvent?.Dispose();
        base.OnExit(e);
    }
}
