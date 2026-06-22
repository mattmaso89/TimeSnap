using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace TimeSnap.Services;

internal sealed class ScreenshotService
{
    private const int JpegQuality = 85;

    private static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

    public string BasePath { get; }

    public ScreenshotService()
    {
        BasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "TimeSnap", "Screenshots");
    }

    public string CaptureAndSave()
    {
        var now = DateTime.Now;
        var filePath = BuildFilePath(now);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // Capture the active window title before BitBlt so we see the user's app, not ours.
        string title = GetForegroundWindowTitle();

        CaptureScreen(filePath);

        if (!string.IsNullOrEmpty(title))
            File.WriteAllText(Path.ChangeExtension(filePath, ".title"), title, System.Text.Encoding.UTF8);

        return filePath;
    }

    private static string GetForegroundWindowTitle()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            var sb = new System.Text.StringBuilder(512);
            int len = NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            return len > 0 ? sb.ToString(0, len) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildFilePath(DateTime now)
    {
        int week = ISOWeek.GetWeekOfYear(now);
        string weekFolder = $"{now.Year}-KW{week:D2}";
        string dayFolder = GetGermanDayName(now.DayOfWeek);
        string fileName = $"{now:HH-mm-ss}.jpg";
        return Path.Combine(BasePath, weekFolder, dayFolder, fileName);
    }

    private static void CaptureScreen(string filePath)
    {
        int width  = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
        IntPtr desktopDC  = NativeMethods.GetDC(desktopWnd);
        IntPtr memDC      = NativeMethods.CreateCompatibleDC(desktopDC);
        IntPtr hBitmap    = NativeMethods.CreateCompatibleBitmap(desktopDC, width, height);
        IntPtr oldBitmap  = NativeMethods.SelectObject(memDC, hBitmap);

        try
        {
            NativeMethods.BitBlt(memDC, 0, 0, width, height, desktopDC, 0, 0, NativeMethods.SRCCOPY);

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
