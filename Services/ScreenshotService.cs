using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace TimeSnap.Services;

internal sealed class ScreenshotService
{
    private const int JpegQuality = 85;

    private static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

    // Read fresh on every capture so a folder change in the settings page
    // takes effect immediately, without restarting the app.
    public string BasePath => SettingsService.EffectiveScreenshotFolder;

    // Captures every monitor currently selected in settings (defaulting to just
    // the primary monitor) and saves one JPEG per monitor. Returns every saved
    // file path.
    public List<string> CaptureAndSave()
    {
        var monitors = MonitorService.GetSelectedMonitors(SettingsService.Current.SelectedMonitorDeviceIds);
        bool multiMonitor = monitors.Count > 1;

        var now = DateTime.Now;
        var savedPaths = new List<string>(monitors.Count);

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            string suffix = multiMonitor ? $"_M{i + 1}" : string.Empty;
            var filePath = BuildFilePath(now, suffix);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // Capture the active window title before BitBlt so we see the user's app, not
            // ours — resolved per monitor, since the foreground app can differ between screens.
            string title = GetForegroundWindowTitle(monitor);

            CaptureScreen(filePath, monitor);

            if (!string.IsNullOrEmpty(title))
                File.WriteAllText(Path.ChangeExtension(filePath, ".title"), title, System.Text.Encoding.UTF8);

            savedPaths.Add(filePath);
        }

        return savedPaths;
    }

    // Returns the title of whichever window is actually in front on the given
    // monitor — the one being captured. GetForegroundWindow() alone reports the
    // system-wide active window, which may sit on a different monitor than the
    // screenshot, causing the displayed app name to not match the image content.
    private static string GetForegroundWindowTitle(MonitorInfo monitor)
    {
        try
        {
            var hwnd = GetMonitorForegroundWindow(monitor);
            if (hwnd == IntPtr.Zero)
                return string.Empty;

            if (IsDesktopWindow(hwnd))
                return TimeSnap.Loc.Get("DesktopWindowTitle");

            var sb = new System.Text.StringBuilder(512);
            int len = NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            return len > 0 ? sb.ToString(0, len) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // Walks the top-level window z-order front-to-back and returns the first
    // window that is visible, not minimized, not a tool window, not cloaked by
    // DWM, and actually located on the given monitor.
    private static IntPtr GetMonitorForegroundWindow(MonitorInfo monitor)
    {
        var center = new NativeMethods.POINT { x = monitor.Left + monitor.Width / 2, y = monitor.Top + monitor.Height / 2 };
        IntPtr targetMonitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);

        IntPtr hwnd = NativeMethods.GetTopWindow(IntPtr.Zero);
        while (hwnd != IntPtr.Zero)
        {
            if (IsEligibleForegroundCandidate(hwnd, targetMonitor))
                return hwnd;

            hwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDNEXT);
        }

        return IntPtr.Zero;
    }

    private static bool IsEligibleForegroundCandidate(IntPtr hwnd, IntPtr targetMonitor)
    {
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
            return false;

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0
            && (exStyle & NativeMethods.WS_EX_APPWINDOW) == 0;
        if (isToolWindow)
            return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            return false;

        // Skip windows cloaked by DWM (e.g. UWP apps parked on another virtual desktop).
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        IntPtr windowMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return windowMonitor == targetMonitor;
    }

    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString() is "Progman" or "WorkerW";
    }

    private string BuildFilePath(DateTime now, string suffix)
    {
        int week = ISOWeek.GetWeekOfYear(now);
        string weekFolder = $"{now.Year}-KW{week:D2}";
        string dayFolder = GetGermanDayName(now.DayOfWeek);
        string fileName = $"{now:HH-mm-ss}{suffix}.jpg";
        return Path.Combine(BasePath, weekFolder, dayFolder, fileName);
    }

    // BitBlt's source coordinates are virtual-screen coordinates, which can be
    // negative for monitors positioned left of or above the primary monitor —
    // the desktop DC spans the full virtual desktop, not just the primary screen.
    private static void CaptureScreen(string filePath, MonitorInfo monitor)
    {
        IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
        IntPtr desktopDC  = NativeMethods.GetDC(desktopWnd);
        IntPtr memDC      = NativeMethods.CreateCompatibleDC(desktopDC);
        IntPtr hBitmap    = NativeMethods.CreateCompatibleBitmap(desktopDC, monitor.Width, monitor.Height);
        IntPtr oldBitmap  = NativeMethods.SelectObject(memDC, hBitmap);

        try
        {
            NativeMethods.BitBlt(memDC, 0, 0, monitor.Width, monitor.Height, desktopDC, monitor.Left, monitor.Top, NativeMethods.SRCCOPY);

            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)JpegQuality);

            using var bmp = Image.FromHbitmap(hBitmap);
            bmp.Save(filePath, JpegCodec, encoderParams);
        }
        finally
        {
            NativeMethods.SelectObject(memDC, oldBitmap);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(memDC);
            NativeMethods.ReleaseDC(desktopWnd, desktopDC);
        }
    }

    private static string GetGermanDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => "Montag",
        DayOfWeek.Tuesday   => "Dienstag",
        DayOfWeek.Wednesday => "Mittwoch",
        DayOfWeek.Thursday  => "Donnerstag",
        DayOfWeek.Friday    => "Freitag",
        DayOfWeek.Saturday  => "Samstag",
        DayOfWeek.Sunday    => "Sonntag",
        _                   => "Unbekannt"
    };
}
