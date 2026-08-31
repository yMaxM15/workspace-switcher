using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.Core.Native;

namespace WorkspaceSwitcher.Core;

public class WindowManager
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Button",
        "Windows.UI.Core.CoreWindow",
        "EdgeUiInputTopWndClass",
        "EdgeUiInputWndClass",
        "ApplicationFrameTitleBarWindow"
    };

    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInputHost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "LockApp",
        "SystemSettings"
    };

    /// <summary>
    /// Captures a snapshot of all currently active and visible application windows.
    /// </summary>
    public WorkspaceProfile CaptureWorkspace(string profileName, string? description = null)
    {
        var profile = new WorkspaceProfile(profileName)
        {
            Description = description,
            Windows = new List<WindowInfo>()
        };

        var shellHwnd = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsValidAppWindow(hWnd, shellHwnd))
            {
                return true; // Continue enumeration
            }

            var windowInfo = ExtractWindowInfo(hWnd);
            if (windowInfo != null)
            {
                profile.Windows.Add(windowInfo);
            }

            return true;
        }, IntPtr.Zero);

        return profile;
    }

    /// <summary>
    /// Restores a given workspace profile by repositioning matching windows.
    /// </summary>
    /// <param name="profile">The profile to restore.</param>
    /// <param name="launchIfNotRunning">If true, launches executable paths for apps that are not currently running.</param>
    /// <returns>Count of successfully repositioned windows.</returns>
    public int RestoreWorkspace(WorkspaceProfile profile, bool launchIfNotRunning = false)
    {
        if (profile == null || profile.Windows == null || profile.Windows.Count == 0)
        {
            return 0;
        }

        // 1. Collect currently open candidate windows
        var currentWindows = GetCurrentOpenWindows();
        var matchedHandles = new HashSet<IntPtr>();
        var restoredCount = 0;

        foreach (var savedWindow in profile.Windows)
        {
            // Try to find matching open window
            var target = FindBestMatch(savedWindow, currentWindows, matchedHandles);

            if (target != null)
            {
                matchedHandles.Add(target.Handle);

                var nativePlacement = savedWindow.Placement.ToNative();

                // Apply placement
                bool success = NativeMethods.SetWindowPlacement(target.Handle, ref nativePlacement);

                if (!success)
                {
                    // Fallback to SetWindowPos if SetWindowPlacement fails
                    var rect = savedWindow.Placement.NormalPosition;
                    NativeMethods.SetWindowPos(
                        target.Handle,
                        IntPtr.Zero,
                        rect.Left,
                        rect.Top,
                        rect.Width,
                        rect.Height,
                        NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW
                    );
                }

                restoredCount++;
            }
            else if (launchIfNotRunning && !string.IsNullOrWhiteSpace(savedWindow.ExecutablePath) && File.Exists(savedWindow.ExecutablePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = savedWindow.ExecutablePath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Ignore launch failures for restricted/UWP apps
                }
            }
        }

        return restoredCount;
    }

    /// <summary>
    /// Retrieves all currently open, valid application windows.
    /// </summary>
    public List<WindowInfo> GetCurrentOpenWindows()
    {
        var list = new List<WindowInfo>();
        var shellHwnd = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (IsValidAppWindow(hWnd, shellHwnd))
            {
                var info = ExtractWindowInfo(hWnd);
                if (info != null)
                {
                    list.Add(info);
                }
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>
    /// Evaluates whether a window is an actual top-level user application window.
    /// Filters out desktop, taskbars, cloaked UWP apps, invisible popups, and tool windows.
    /// </summary>
    public static bool IsValidAppWindow(IntPtr hWnd, IntPtr shellHwnd)
    {
        if (hWnd == IntPtr.Zero || hWnd == shellHwnd)
            return false;

        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Filter out cloaked windows (e.g. suspended UWP apps or windows on other virtual desktops)
        int cloakedVal = 0;
        int hr = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out cloakedVal, sizeof(int));
        if (hr == 0 && cloakedVal != 0)
        {
            return false;
        }

        // Filter by Window Title length
        int textLength = NativeMethods.GetWindowTextLength(hWnd);
        if (textLength == 0)
            return false;

        // Filter by Extended Window Styles
        long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;

        if (isToolWindow && !isAppWindow)
            return false;

        // Filter by Class Name
        var classSb = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, classSb, classSb.Capacity);
        string className = classSb.ToString();

        if (IgnoredClasses.Contains(className))
            return false;

        // Filter by Window Dimensions (ignore 0-sized helper windows)
        if (NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return false;
        }

        return true;
    }

    private WindowInfo? ExtractWindowInfo(IntPtr hWnd)
    {
        try
        {
            // Title
            var titleSb = new StringBuilder(512);
            NativeMethods.GetWindowText(hWnd, titleSb, titleSb.Capacity);
            string title = titleSb.ToString();

            // Class Name
            var classSb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, classSb, classSb.Capacity);
            string className = classSb.ToString();

            // Process & Executable Path
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = string.Empty;
            string? exePath = null;

            if (processId != 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)processId);
                    processName = proc.ProcessName;

                    if (IgnoredProcesses.Contains(processName))
                        return null;

                    exePath = GetProcessExecutablePath(processId, proc);
                }
                catch
                {
                    // In case process terminated or access is denied
                }
            }

            // Window Placement
            var wp = NativeMethods.WINDOWPLACEMENT.Create();
            if (!NativeMethods.GetWindowPlacement(hWnd, ref wp))
            {
                return null;
            }

            // Window Rect
            NativeMethods.GetWindowRect(hWnd, out var rect);

            return new WindowInfo
            {
                Handle = hWnd,
                ProcessId = processId,
                ProcessName = processName,
                ExecutablePath = exePath,
                WindowTitle = title,
                ClassName = className,
                Placement = WindowPlacementInfo.FromNative(wp),
                Bounds = WindowRect.FromNative(rect)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProcessExecutablePath(uint processId, Process proc)
    {
        // Try QueryFullProcessImageName (bypasses 32/64 bit mismatch issues)
        IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        // Fallback to Process.MainModule
        try
        {
            return proc.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static WindowInfo? FindBestMatch(
        WindowInfo savedWindow,
        List<WindowInfo> currentWindows,
        HashSet<IntPtr> alreadyMatched)
    {
        var candidates = currentWindows.Where(w => !alreadyMatched.Contains(w.Handle)).ToList();

        // 1. Exact Match: ProcessName + WindowTitle + ClassName
        var match = candidates.FirstOrDefault(w =>
            string.Equals(w.ProcessName, savedWindow.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(w.WindowTitle, savedWindow.WindowTitle, StringComparison.Ordinal) &&
            string.Equals(w.ClassName, savedWindow.ClassName, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 2. Strong Match: ProcessName + WindowTitle
        match = candidates.FirstOrDefault(w =>
            string.Equals(w.ProcessName, savedWindow.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(w.WindowTitle, savedWindow.WindowTitle, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 3. Partial Match: ProcessName + WindowTitle starts with / contains
        match = candidates.FirstOrDefault(w =>
            string.Equals(w.ProcessName, savedWindow.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            (w.WindowTitle.Contains(savedWindow.WindowTitle, StringComparison.OrdinalIgnoreCase) ||
             savedWindow.WindowTitle.Contains(w.WindowTitle, StringComparison.OrdinalIgnoreCase)));

        if (match != null) return match;

        // 4. Fallback: First available window of the same process
        match = candidates.FirstOrDefault(w =>
            string.Equals(w.ProcessName, savedWindow.ProcessName, StringComparison.OrdinalIgnoreCase));

        return match;
    }
}
