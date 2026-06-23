using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimeSnap.Services;

/// <summary>
/// Registers a configurable global hotkey via Win32 RegisterHotKey.
/// Registration and unregistration happen on the MessageWindow thread,
/// as Win32 requires the caller thread to own the target HWND.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private const int TestHotkeyId = 0xCAFE;

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

    // Registers (modifiers, virtualKey) as the live global hotkey, replacing
    // whatever combination was registered before. Safe to call again later to
    // swap the hotkey at runtime — the previous registration is released first.
    public bool Register(uint modifiers, uint virtualKey)
    {
        bool ok = false;

        _messageWindow.Invoke(() =>
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
                _registered = false;
            }

            ok = NativeMethods.RegisterHotKey(
                _messageWindow.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            _registered = ok;

            Debug.WriteLine(ok
                ? $"[HotkeyService] RegisterHotKey ERFOLGREICH (mods=0x{modifiers:X}, vk=0x{virtualKey:X})."
                : $"[HotkeyService] RegisterHotKey FEHLGESCHLAGEN – Fehlercode {Marshal.GetLastWin32Error()}");
        });

        return ok;
    }

    // Tests whether (modifiers, virtualKey) could be registered as a global
    // hotkey right now — i.e. it isn't already claimed by another application.
    // Registers and immediately unregisters again; never touches the app's
    // actual live hotkey (registered separately on the MessageWindow thread).
    public static bool IsAvailable(uint modifiers, uint virtualKey, out int win32Error)
    {
        bool ok = NativeMethods.RegisterHotKey(IntPtr.Zero, TestHotkeyId, modifiers, virtualKey);
        win32Error = ok ? 0 : Marshal.GetLastWin32Error();

        if (ok)
            NativeMethods.UnregisterHotKey(IntPtr.Zero, TestHotkeyId);

        return ok;
    }

    public void Dispose()
    {
        if (_registered)
        {
            _messageWindow.Invoke(() => NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId));
            _registered = false;
        }
    }
}
