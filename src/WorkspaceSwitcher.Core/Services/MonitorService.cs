using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WorkspaceSwitcher.Core.Native;

namespace WorkspaceSwitcher.Core.Services;

public class MonitorOption
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }

    public override string ToString() => Name;
}

public static class MonitorService
{
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    private const uint MONITORINFOF_PRIMARY = 1;

    public static List<MonitorOption> GetConnectedMonitors()
    {
        var monitors = new List<MonitorOption>();
        int count = 1;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
                int h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

                monitors.Add(new MonitorOption
                {
                    Index = count,
                    Name = isPrimary 
                        ? $"Monitor {count} (Primary - {w}×{h})" 
                        : $"Monitor {count} ({w}×{h} at {mi.rcMonitor.Left},{mi.rcMonitor.Top})",
                    Left = mi.rcMonitor.Left,
                    Top = mi.rcMonitor.Top,
                    Width = w,
                    Height = h,
                    IsPrimary = isPrimary
                });
                count++;
            }
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(new MonitorOption
            {
                Index = 1,
                Name = "Monitor 1 (1920×1080)",
                Left = 0,
                Top = 0,
                Width = 1920,
                Height = 1080,
                IsPrimary = true
            });
        }

        return monitors;
    }
}
