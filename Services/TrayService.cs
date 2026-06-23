using System.Runtime.InteropServices;

namespace TimeSnap.Services;

internal sealed class TrayService : IDisposable
{
    private const int IconId = 1;

    private NativeMethods.NOTIFYICONDATA _nid;
    private bool _disposed;

    public event Action? ShowWindowRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayService(MessageWindow messageWindow, string iconPath)
    {
        messageWindow.TrayIconMessage += HandleTrayMessage;

        var hIcon = NativeMethods.LoadImage(IntPtr.Zero, iconPath,
            NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);

        if (hIcon == IntPtr.Zero)
            hIcon = NativeMethods.LoadIcon(IntPtr.Zero, new IntPtr(32512));

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize          = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd            = messageWindow.Handle,
            uID             = IconId,
            uFlags          = NativeMethods.NIF_ICON | NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon           = hIcon,
            szTip           = BuildTooltip()
        };

        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
    }

    // Reflects the currently configured hotkey in the tray tooltip — called
    // again after the settings page registers a new one at runtime.
    public void UpdateTooltip()
    {
        _nid.szTip = BuildTooltip();
        _nid.uFlags = NativeMethods.NIF_TIP;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
    }

    private static string BuildTooltip()
    {
        string hotkeyText = HotkeyFormat.Format(SettingsService.Current.HotkeyModifiers, SettingsService.Current.HotkeyVirtualKey);
        return string.Format(TimeSnap.Loc.Get("TrayTooltip"), hotkeyText);
    }

    private void HandleTrayMessage(int iconId, uint mouseMsg)
    {
        switch (mouseMsg)
        {
            case NativeMethods.WM_LBUTTONDBLCLK:
                ShowWindowRequested?.Invoke();
                break;

            case NativeMethods.WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var hMenu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(hMenu, 0, new IntPtr(1), TimeSnap.Loc.Get("MenuOpen"));
        NativeMethods.AppendMenu(hMenu, 0, new IntPtr(3), TimeSnap.Loc.Get("MenuSettings"));
        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
        NativeMethods.AppendMenu(hMenu, 0, new IntPtr(2), TimeSnap.Loc.Get("MenuExit"));

        NativeMethods.GetCursorPos(out var pt);
        NativeMethods.SetForegroundWindow(_nid.hWnd);

        var cmd = NativeMethods.TrackPopupMenu(
            hMenu,
            NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD,
            pt.x, pt.y, 0, _nid.hWnd, IntPtr.Zero);

        NativeMethods.DestroyMenu(hMenu);

        switch (cmd)
        {
            case 1: ShowWindowRequested?.Invoke(); break;
            case 2: ExitRequested?.Invoke(); break;
            case 3: SettingsRequested?.Invoke(); break;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            _disposed = true;
        }
    }
}
