using System.Runtime.InteropServices;

namespace TimeSnap.Services;

internal sealed record MonitorInfo(string DeviceId, int Left, int Top, int Width, int Height, bool IsPrimary);

internal static class MonitorService
{
    // Enumerates all currently connected monitors, ordered top-to-bottom then
    // left-to-right so numbering ("Bildschirm 1", "Bildschirm 2", ...) stays
    // stable and predictable across calls.
    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT _2, IntPtr _3) =>
            {
                var info = new NativeMethods.MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    monitors.Add(new MonitorInfo(
                        DeviceId: info.szDevice,
                        Left: info.rcMonitor.Left,
                        Top: info.rcMonitor.Top,
                        Width: info.rcMonitor.Right - info.rcMonitor.Left,
                        Height: info.rcMonitor.Bottom - info.rcMonitor.Top,
                        IsPrimary: (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                }
                return true;
            }, IntPtr.Zero);

        return [.. monitors.OrderBy(m => m.Top).ThenBy(m => m.Left)];
    }

    // Resolves the monitors to actually capture for a screenshot: the ones whose
    // device IDs are in selectedDeviceIds, or — if that's empty (not configured
    // yet) or none of the saved IDs match a currently connected monitor — just
    // the primary monitor, matching TimeSnap's original single-screen behavior.
    public static List<MonitorInfo> GetSelectedMonitors(IReadOnlyList<string> selectedDeviceIds)
    {
        var all = GetMonitors();

        if (selectedDeviceIds.Count > 0)
        {
            var selected = all.Where(m => selectedDeviceIds.Contains(m.DeviceId)).ToList();
            if (selected.Count > 0)
                return selected;
        }

        return all.Where(m => m.IsPrimary).ToList();
    }
}
