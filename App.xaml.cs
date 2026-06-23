using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TimeSnap.Services;

namespace TimeSnap;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private MessageWindow? _messageWindow;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private ScreenshotService? _screenshotService;
    private DispatcherQueue? _dispatcherQueue;

    public App()
    {
        // Must run before any resource (.resw) or culture-dependent formatting is
        // touched, so this happens before InitializeComponent().
        ApplyLanguageOverride(SettingsService.Current.Language);
        InitializeComponent();
    }

    // Forces the app's display language ("de"/"en"), or clears the override to
    // follow the Windows system language ("System"). Only takes full effect on
    // the next app start — see SettingsPage's restart prompt.
    //
    // Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride requires
    // an MSIX package identity and throws in this unpackaged app, so the
    // override is applied via Loc's own ResourceContext qualifier instead, plus
    // CultureInfo for the date/day-name formatting in MainPage.
    private static void ApplyLanguageOverride(string language)
    {
        string? bcp47 = language switch
        {
            "de" => "de-DE",
            "en" => "en-US",
            _ => null
        };

        Loc.SetLanguageOverride(bcp47);

        if (bcp47 is not null)
        {
            var culture = new CultureInfo(bcp47);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }

    // Relaunches a new instance of the app and exits this one — used after a
    // language change, since .resw resources only re-resolve at process start.
    public void RestartApp()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                Process.Start(exePath);
        }
        finally
        {
            ExitApp();
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _mainWindow = new MainWindow();
        ApplyTheme(SettingsService.Current.Theme);

        _messageWindow = new MessageWindow();
        _messageWindow.Start();

        string iconPath = GetIconPath();

        _trayService = new TrayService(_messageWindow, iconPath);
        _trayService.ShowWindowRequested += () => _dispatcherQueue.TryEnqueue(ShowMainWindow);
        _trayService.SettingsRequested += () => _dispatcherQueue.TryEnqueue(ShowSettingsWindow);
        _trayService.ExitRequested += () => _dispatcherQueue.TryEnqueue(ExitApp);

        _hotkeyService = new HotkeyService(_messageWindow);
        _hotkeyService.HotkeyPressed += () =>
        {
            System.Diagnostics.Debug.WriteLine("[App] HotkeyPressed → DispatcherQueue.TryEnqueue(TakeScreenshot)");
            _dispatcherQueue.TryEnqueue(TakeScreenshot);
        };
        bool hotkeyOk = _hotkeyService.Register(SettingsService.Current.HotkeyModifiers, SettingsService.Current.HotkeyVirtualKey);
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

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            ApplyTheme(SettingsService.Current.Theme);
        }

        _settingsWindow.AppWindow.Show();
        _settingsWindow.Activate();
    }

    // Applies the given theme ("System"/"Light"/"Dark") to every currently open
    // window so the change takes effect immediately, without an app restart.
    public void ApplyTheme(string themeSetting)
    {
        var theme = themeSetting switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (_mainWindow?.Content is FrameworkElement mainRoot)
            mainRoot.RequestedTheme = theme;

        if (_settingsWindow?.Content is FrameworkElement settingsRoot)
            settingsRoot.RequestedTheme = theme;
    }

    // Fired on the UI thread after every successful screenshot (path = saved .jpg).
    public event Action<string>? ScreenshotSaved;

    // Fired on the UI thread after the settings page deletes all screenshots from disk.
    public event Action? ScreenshotsCleared;

    public void NotifyScreenshotsCleared() => ScreenshotsCleared?.Invoke();

    // Fired on the UI thread after the settings page changes the screenshot storage folder.
    public event Action? ScreenshotFolderChanged;

    public void NotifyScreenshotFolderChanged() => ScreenshotFolderChanged?.Invoke();

    // Fired on the UI thread after the settings page changes weekend visibility.
    public event Action? ShowWeekendChanged;

    public void NotifyShowWeekendChanged() => ShowWeekendChanged?.Invoke();

    // Swaps the live global hotkey at runtime — no app restart needed.
    public bool ReregisterHotkey(uint modifiers, uint virtualKey)
    {
        bool ok = _hotkeyService?.Register(modifiers, virtualKey) ?? false;
        if (ok)
            _trayService?.UpdateTooltip();
        return ok;
    }

    private void TakeScreenshot()
    {
        Task.Run(() =>
        {
            try
            {
                var paths = _screenshotService!.CaptureAndSave();

                // One flash per captured monitor, positioned/sized to match it.
                foreach (var monitor in MonitorService.GetSelectedMonitors(SettingsService.Current.SelectedMonitorDeviceIds))
                    FlashService.Show(monitor.Left, monitor.Top, monitor.Width, monitor.Height);

                if (SettingsService.Current.SoundEnabled)
                    SoundService.PlayCameraClick();
                System.Diagnostics.Debug.WriteLine($"[TimeSnap] Screenshots gespeichert: {string.Join(", ", paths)}");
                _dispatcherQueue!.TryEnqueue(() =>
                {
                    foreach (var path in paths)
                        ScreenshotSaved?.Invoke(path);
                });
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
