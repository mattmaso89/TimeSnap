using TimeSnap.Services;

namespace TimeSnap;

// Plain data model persisted by Services.SettingsService.
internal sealed class AppSettings
{
    public static string DefaultScreenshotFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "TimeSnap", "Screenshots");

    public const uint DefaultHotkeyModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT;
    public const uint DefaultHotkeyVirtualKey = NativeMethods.VK_Y;

    // Win32 MOD_* flags and a virtual-key code — locale-independent storage,
    // unlike a display string such as "Strg+Alt+Shift+Y".
    public uint HotkeyModifiers { get; set; } = DefaultHotkeyModifiers;
    public uint HotkeyVirtualKey { get; set; } = DefaultHotkeyVirtualKey;

    public bool SoundEnabled { get; set; } = false;
    public bool ShowWeekend { get; set; } = false;

    // Device IDs (e.g. "\\.\DISPLAY1") of the monitors to capture on hotkey press.
    // Empty = not configured yet, or none of the saved IDs match a currently
    // connected monitor — MonitorService then falls back to the primary monitor
    // only, matching TimeSnap's original single-screen behavior.
    public List<string> SelectedMonitorDeviceIds { get; set; } = [];
    public string Language { get; set; } = "System";
    public string ScreenshotFolder { get; set; } = DefaultScreenshotFolder;
    public string Theme { get; set; } = "System";
}
