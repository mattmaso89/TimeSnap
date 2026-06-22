using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeSnap.Services;

// Displays a brief white border-ring at the screen edges (~200 ms) to confirm a screenshot was taken.
// Runs on its own short-lived STA thread; never blocks the UI or the capture thread.
internal static class FlashService
{
    private const string   ClassName    = "TimeSnapFlash";
    private const int      BorderPx     = 5;
    private const int      DurationMs   = 200;
    private const byte     MaxAlpha     = 210;

    // ExStyle: TOPMOST | LAYERED | TRANSPARENT (click-through) | NOACTIVATE
    private const uint FlashExStyle =
        NativeMethods.WS_EX_TOPMOST_FLAG     |
        NativeMethods.WS_EX_LAYERED_FLAG     |
        NativeMethods.WS_EX_TRANSPARENT_FLAG |
        NativeMethods.WS_EX_NOACTIVATE_FLAG;

    private const uint FlashStyle =
        NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE;

    // Static delegate + lock keep the WndProc and WNDCLASSEX registration alive for the process lifetime.
    private static readonly NativeMethods.WndProcDelegate _wndProc =
        static (hwnd, msg, wp, lp) => NativeMethods.DefWindowProc(hwnd, msg, wp, lp);

    private static readonly object _classLock = new();
    private static IntPtr _whiteBrush;
    private static bool   _classRegistered;

    public static void Show()
    {
        var thread = new Thread(RunFlash) { IsBackground = true, Name = "TimeSnap-Flash" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void EnsureWindowClass()
    {
        lock (_classLock)
        {
            if (_classRegistered) return;

            _whiteBrush = NativeMethods.CreateSolidBrush(0x00FFFFFF); // white in BGR

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize        = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance     = NativeMethods.GetModuleHandle(null),
                hbrBackground = _whiteBrush,
                lpszClassName = ClassName
            };

            NativeMethods.RegisterClassEx(ref wc); // failure = already registered; both paths are fine
            _classRegistered = true;
        }
    }

    private static void RunFlash()
    {
        EnsureWindowClass();

        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        var hwnd = NativeMethods.CreateWindowEx(
            FlashExStyle, ClassName, "",
            FlashStyle, 0, 0, w, h,
            IntPtr.Zero, IntPtr.Zero, NativeMethods.GetModuleHandle(null), IntPtr.Zero);

        if (hwnd == IntPtr.Zero) return;

        // Carve out the window interior — only the border ring remains visible.
        var outer = NativeMethods.CreateRectRgn(0, 0, w, h);
        var inner = NativeMethods.CreateRectRgn(BorderPx, BorderPx, w - BorderPx, h - BorderPx);
        NativeMethods.CombineRgn(outer, outer, inner, NativeMethods.RGN_DIFF);
        NativeMethods.DeleteObject(inner);
        NativeMethods.SetWindowRgn(hwnd, outer, true); // OS takes ownership of outer

        NativeMethods.SetLayeredWindowAttributes(hwnd, 0, MaxAlpha, NativeMethods.LWA_ALPHA);

        // Initial paint pass
        Pump(hwnd);

        // Fade out over DurationMs
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < DurationMs)
        {
            Pump(IntPtr.Zero);
            double t     = Math.Min(sw.ElapsedMilliseconds / (double)DurationMs, 1.0);
            byte   alpha = (byte)(MaxAlpha * (1.0 - t));
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, alpha, NativeMethods.LWA_ALPHA);
            Thread.Sleep(12);
        }

        NativeMethods.DestroyWindow(hwnd);
    }

    private static void Pump(IntPtr hwnd)
    {
        while (NativeMethods.PeekMessage(out var msg, hwnd, 0, 0, NativeMethods.PM_REMOVE))
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }
}
