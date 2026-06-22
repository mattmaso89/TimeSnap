using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeSnap.Services;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+S) via Win32 RegisterHotKey.
/// Registration and unregistration happen on the MessageWindow thread,
/// as Win32 requires the caller thread to own the target HWND.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;

    private readonly MessageWindow _messageWindow;
    private bool _registered;

    public event Action? HotkeyPressed;

    public HotkeyService(MessageWindow messageWindow)
    {
        _messageWindow = messageWindow;
        _messageWindow.HotkeyPressed += () =>
        {
            Debug.WriteLine("[HotkeyService] HotkeyPressed – leite weiter an Subscriber.");
            HotkeyPressed?.Invoke();
        };
    }

    public bool Register()
    {
        if (_registered) return true;

        IntPtr hwnd = _messageWindow.Handle;
        uint mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;

        Debug.WriteLine($"[HotkeyService] Registriere Hotkey auf MessageWindow-Thread " +
                        $"(HWND=0x{hwnd:X}, mods=0x{mods:X}, vk=0x{NativeMethods.VK_Y:X})...");

        // RegisterHotKey must be called from the thread that owns the HWND.
        _messageWindow.Invoke(() =>
        {
            _registered = NativeMethods.RegisterHotKey(hwnd, HotkeyId, mods, NativeMethods.VK_Y);

            if (_registered)
            {
                Debug.WriteLine("[HotkeyService] RegisterHotKey ERFOLGREICH – Strg+Shift+S aktiv.");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                string hint = err switch
                {
                    1409 => "ERROR_HOTKEY_ALREADY_REGISTERED – von anderer App belegt",
                    1408 => "ERROR_INVALID_WINDOW_HANDLE – falscher Thread (sollte jetzt behoben sein)",
                    5    => "ERROR_ACCESS_DENIED",
                    87   => "ERROR_INVALID_PARAMETER",
                    _    => $"Fehlercode {err}"
                };
                Debug.WriteLine($"[HotkeyService] RegisterHotKey FEHLGESCHLAGEN – {hint}");
            }
        });

        return _registered;
    }

    public void Dispose()
    {
        if (_registered)
        {
            _messageWindow.Invoke(() =>
            {
                NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
                Debug.WriteLine("[HotkeyService] Hotkey deregistriert.");
            });
            _registered = false;
        }
    }
}
