using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeSnap.Services;

/// <summary>
/// Hidden Win32 message-only window running on a dedicated STA thread.
/// Receives WM_HOTKEY and tray icon notifications and fires C# events.
/// Use Invoke() to marshal any Win32 call that must run on this thread
/// (e.g. RegisterHotKey, which requires the caller thread == window owner thread).
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    // Custom message: dequeue and execute a pending Action on this thread.
    private const uint WM_INVOKE = NativeMethods.WM_APP + 2;

    private Thread? _thread;
    private volatile IntPtr _hwnd = IntPtr.Zero;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private NativeMethods.WndProcDelegate? _wndProcDelegate; // kept alive to prevent GC

    public IntPtr Handle => _hwnd;

    public event Action? HotkeyPressed;
    public event Action<int, uint>? TrayIconMessage;

    public void Start()
    {
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "TimeSnap-MessageWindow"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        bool signaled = _ready.Wait(TimeSpan.FromSeconds(5));
        Debug.WriteLine($"[MessageWindow] Start() – signaled={signaled}, HWND=0x{_hwnd:X}");

        if (_hwnd == IntPtr.Zero)
            Debug.WriteLine("[MessageWindow] WARNING: HWND ist zero – Fenstererstellung fehlgeschlagen!");
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the MessageWindow thread and blocks until it completes.
    /// Required for any Win32 call that must originate from the thread owning the HWND
    /// (RegisterHotKey, UnregisterHotKey, etc.).
    /// </summary>
    public void Invoke(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingActions.Enqueue(() =>
        {
            try   { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        NativeMethods.PostMessage(_hwnd, WM_INVOKE, IntPtr.Zero, IntPtr.Zero);
        tcs.Task.GetAwaiter().GetResult(); // short block, message loop is already running
    }

    public void Stop()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void RunMessageLoop()
    {
        _wndProcDelegate = WndProc;

        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = "TimeSnapMsgWnd"
        };

        ushort atom = NativeMethods.RegisterClassEx(ref wc);
        Debug.WriteLine($"[MessageWindow] RegisterClassEx atom={atom}, LastError={Marshal.GetLastWin32Error()}");

        _hwnd = NativeMethods.CreateWindowEx(
            0, "TimeSnapMsgWnd", "TimeSnap",
            0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        Debug.WriteLine($"[MessageWindow] CreateWindowEx HWND=0x{_hwnd:X}, LastError={Marshal.GetLastWin32Error()}");

        _ready.Set();

        Debug.WriteLine("[MessageWindow] Message-Loop gestartet.");

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        Debug.WriteLine("[MessageWindow] Message-Loop beendet.");
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INVOKE:
                if (_pendingActions.TryDequeue(out var action))
                    action();
                return IntPtr.Zero;

            case NativeMethods.WM_HOTKEY:
                Debug.WriteLine($"[MessageWindow] WM_HOTKEY empfangen! id={wParam}");
                HotkeyPressed?.Invoke();
                return IntPtr.Zero;

            case NativeMethods.WM_TRAYICON:
                var mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                TrayIconMessage?.Invoke((int)wParam, mouseMsg);
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                Debug.WriteLine("[MessageWindow] WM_DESTROY empfangen.");
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose() => Stop();
}
