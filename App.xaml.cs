using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TimeSnap.Services;

namespace TimeSnap;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MessageWindow? _messageWindow;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private ScreenshotService? _screenshotService;
    private DispatcherQueue? _dispatcherQueue;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _mainWindow = new MainWindow();

        _messageWindow = new MessageWindow();
        _messageWindow.Start();

        string iconPath = GetIconPath();

        _trayService = new TrayService(_messageWindow, iconPath);
        _trayService.ShowWindowRequested += () => _dispatcherQueue.TryEnqueue(ShowMainWindow);
        _trayService.ExitRequested += () => _dispatcherQueue.TryEnqueue(ExitApp);

        _hotkeyService = new HotkeyService(_messageWindow);
        _hotkeyService.HotkeyPressed += () =>
        {
            System.Diagnostics.Debug.WriteLine("[App] HotkeyPressed → DispatcherQueue.TryEnqueue(TakeScreenshot)");
            _dispatcherQueue.TryEnqueue(TakeScreenshot);
        };
        bool hotkeyOk = _hotkeyService.Register();
        System.Diagnostics.Debug.WriteLine($"[App] Hotkey-Registrierung abgeschlossen: {(hotkeyOk ? "ERFOLGREICH" : "FEHLGESCHLAGEN")}");

        _screenshotService = new ScreenshotService();
        System.Diagnostics.Debug.WriteLine($"[App] Screenshot-Zielordner: {_screenshotService.BasePath}");
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
            _mainWindow = new MainWindow();

        _mainWindow.AppWindow.Show();
        _mainWindow.Activate();
    }

    // Fired on the UI thread after every successful screenshot (path = saved .jpg).
    public event Action<string>? ScreenshotSaved;

    private void TakeScreenshot()
    {
        Task.Run(() =>
        {
            try
            {
                var path = _screenshotService!.CaptureAndSave();
                FlashService.Show();
                System.Diagnostics.Debug.WriteLine($"[TimeSnap] Screenshot gespeichert: {path}");
                _dispatcherQueue!.TryEnqueue(() => ScreenshotSaved?.Invoke(path));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TimeSnap] Screenshot-Fehler: {ex.Message}");
            }
        });
    }

    private void ExitApp()
    {
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _messageWindow?.Stop();
        Exit();
    }

    private static string GetIconPath()
    {
        try
        {
#pragma warning disable CA1416
            return Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledPath,
                "Assets", "AppIcon.ico");
#pragma warning restore CA1416
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        }
    }
}
