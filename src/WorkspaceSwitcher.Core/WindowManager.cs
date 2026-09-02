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
        "SystemSettings",
        "WorkspaceSwitcher",
        "WorkspaceSwitcher.UI",
        "WorkspaceSwitcher.Cli"
    };

    static WindowManager()
    {
        try
        {
            // Enable Per-Monitor V2 DPI awareness so multi-monitor coordinates are never virtualized
            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Fallback for older Windows builds
        }
    }

    /// <summary>
    /// Captures a snapshot of all currently active and visible application windows.
    /// Excludes the switcher application itself, desktop shells, and background services.
    /// Optionally captures the Windows taskbar pinned items layout.
    /// </summary>
    public WorkspaceProfile CaptureWorkspace(
        string profileName, 
        string? description = null, 
        string? iconGlyph = null, 
        string? hotkeyModifier = null, 
        string? hotkeyKey = null,
        bool captureTaskbar = true,
        IEnumerable<string>? staticAppIdentifiers = null)
    {
        var profile = new WorkspaceProfile(profileName)
        {
            Description = description,
            IconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? "💻" : iconGlyph,
            HotkeyModifier = string.IsNullOrWhiteSpace(hotkeyModifier) ? "Ctrl + Alt" : hotkeyModifier,
            HotkeyKey = hotkeyKey,
            Windows = new List<WindowInfo>()
        };

        if (captureTaskbar)
        {
            try
            {
                var taskbarService = new Services.TaskbarService();
                profile.Taskbar = taskbarService.CaptureCurrentTaskbar(staticAppIdentifiers);
            }
            catch { }
        }

        var shellHwnd = NativeMethods.GetShellWindow();
        int currentPid = Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsValidAppWindow(hWnd, shellHwnd, currentPid))
            {
                return true; // Continue enumeration
            }

            var windowInfo = ExtractWindowInfo(hWnd, currentPid);
            if (windowInfo != null)
            {
                profile.Windows.Add(windowInfo);
            }

            return true;
        }, IntPtr.Zero);

        return profile;
    }

    /// <summary>
    /// Restores a given workspace profile by repositioning matching windows across monitors.
    /// </summary>
    public int RestoreWorkspace(WorkspaceProfile profile, bool launchIfNotRunning = false)
    {
        var (restored, _) = RestoreWorkspace(profile, launchIfNotRunning, previousProfile: null, closeAppsOnSwitch: false);
        return restored;
    }

    /// <summary>
    /// Restores a given workspace profile by repositioning matching windows across monitors.
    /// Optionally launches closed apps and closes apps from the previous workspace that are not in the new layout.
    /// </summary>
    public (int RestoredCount, int ClosedCount) RestoreWorkspace(
        WorkspaceProfile profile, 
        bool launchIfNotRunning = false,
        WorkspaceProfile? previousProfile = null,
        bool closeAppsOnSwitch = false,
        bool switchTaskbarPins = false,
        IEnumerable<string>? staticAppIdentifiers = null)
    {
        if (profile == null)
        {
            return (0, 0);
        }

        // Apply taskbar pinned applications if enabled for this switch
        if (switchTaskbarPins && profile.Taskbar != null && profile.Taskbar.Enabled)
        {
            try
            {
                var taskbarService = new Services.TaskbarService();
                taskbarService.ApplyTaskbar(profile.Taskbar, staticAppIdentifiers);
                System.Threading.Thread.Sleep(300);
            }
            catch { }
        }

        if (profile.Windows == null || profile.Windows.Count == 0)
        {
            return (0, 0);
        }

        var currentWindows = GetCurrentOpenWindows();
        var matchedHandles = new HashSet<IntPtr>();
        var restoredCount = 0;
        var closedCount = 0;

        var pendingLaunchWindows = new List<WindowInfo>();

        foreach (var savedWindow in profile.Windows)
        {
            if (IgnoredProcesses.Contains(savedWindow.ProcessName))
            {
                continue; // Never reposition or launch ignored/own processes
            }

            var target = FindBestMatch(savedWindow, currentWindows, matchedHandles);

            if (target != null)
            {
                matchedHandles.Add(target.Handle);

                if (MoveWindowToPlacement(target.Handle, savedWindow.Placement, savedWindow.Bounds))
                {
                    restoredCount++;
                }
            }
            else if (launchIfNotRunning)
            {
                pendingLaunchWindows.Add(savedWindow);
            }
        }

        // Asynchronously launch missing apps and reposition their windows as soon as they appear
        if (pendingLaunchWindows.Count > 0)
        {
            var successfullyLaunched = new List<WindowInfo>();
            foreach (var pending in pendingLaunchWindows)
            {
                if (LaunchApp(pending))
                {
                    successfullyLaunched.Add(pending);
                }
            }

            if (successfullyLaunched.Count > 0)
            {
                WatchAndPositionPendingWindowsAsync(successfullyLaunched, matchedHandles);
            }
        }

        // Close windows from previous workspace that are not in the new workspace
        if (closeAppsOnSwitch && previousProfile != null && previousProfile.Windows != null &&
            !string.Equals(previousProfile.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            closedCount = CloseOldWorkspaceWindows(previousProfile, matchedHandles, currentWindows);
        }

        return (restoredCount, closedCount);
    }

    /// <summary>
    /// Launches an application process for a window.
    /// Handles execution aliases (e.g. wt.exe for Windows Terminal) and avoids protected directories.
    /// </summary>
    public static bool LaunchApp(WindowInfo window)
    {
        var launchExe = WorkspaceSwitcher.Core.Services.AppIdentityHelper.ResolveLaunchableExecutable(window.ProcessName, window.ExecutablePath) 
                        ?? window.ExecutablePath;

        if (string.IsNullOrWhiteSpace(launchExe))
        {
            launchExe = window.ProcessName;
        }

        try
        {
            string? workingDir = null;
            if (File.Exists(launchExe))
            {
                var dir = Path.GetDirectoryName(launchExe);
                if (!string.IsNullOrEmpty(dir) && !dir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                {
                    workingDir = dir;
                }
            }

            if (string.IsNullOrEmpty(workingDir))
            {
                workingDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = launchExe,
                WorkingDirectory = workingDir,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            if (WorkspaceSwitcher.Core.Services.AppIdentityHelper.IsTerminal(window.ProcessName))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "wt.exe",
                        UseShellExecute = true
                    });
                    return true;
                }
                catch { }
            }
            return false;
        }
    }

    /// <summary>
    /// Watches for newly launched windows to appear and positions them according to their saved workspace layout.
    /// </summary>
    private void WatchAndPositionPendingWindowsAsync(List<WindowInfo> pendingWindows, HashSet<IntPtr> alreadyMatchedHandles)
    {
        System.Threading.Tasks.Task.Run(async () =>
        {
            var remaining = new List<WindowInfo>(pendingWindows);
            var matched = new HashSet<IntPtr>(alreadyMatchedHandles);
            var startTime = DateTime.UtcNow;
            var maxWait = TimeSpan.FromSeconds(12);

            while (remaining.Count > 0 && DateTime.UtcNow - startTime < maxWait)
            {
                await System.Threading.Tasks.Task.Delay(250).ConfigureAwait(false);

                var currentWindows = GetCurrentOpenWindows();
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    var saved = remaining[i];
                    var target = FindBestMatch(saved, currentWindows, matched);
                    if (target != null)
                    {
                        matched.Add(target.Handle);
                        MoveWindowToPlacement(target.Handle, saved.Placement, saved.Bounds);

                        // Some apps (e.g. Firefox, Terminal) adjust bounds during initial startup/session restore.
                        // Re-enforce placement after short delays.
                        var handle = target.Handle;
                        var placement = saved.Placement;
                        var bounds = saved.Bounds;
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(300).ConfigureAwait(false);
                            MoveWindowToPlacement(handle, placement, bounds);
                            await System.Threading.Tasks.Task.Delay(600).ConfigureAwait(false);
                            MoveWindowToPlacement(handle, placement, bounds);
                        });

                        remaining.RemoveAt(i);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Closes any open windows that belonged to the previous workspace and were not claimed by the new workspace.
    /// </summary>
    private int CloseOldWorkspaceWindows(
        WorkspaceProfile previousProfile, 
        HashSet<IntPtr> keptHandles, 
        List<WindowInfo> currentWindows)
    {
        if (previousProfile?.Windows == null || previousProfile.Windows.Count == 0)
        {
            return 0;
        }

        int closed = 0;
        var handledHandles = new HashSet<IntPtr>(keptHandles);

        foreach (var oldWindow in previousProfile.Windows)
        {
            if (IgnoredProcesses.Contains(oldWindow.ProcessName))
            {
                continue;
            }

            var match = FindBestMatch(oldWindow, currentWindows, handledHandles);
            if (match != null)
            {
                handledHandles.Add(match.Handle);
                try
                {
                    NativeMethods.PostMessage(match.Handle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    closed++;
                }
                catch
                {
                    // Ignore failure to post WM_CLOSE
                }
            }
        }

        return closed;
    }

    public static bool MoveWindowToPlacement(IntPtr hWnd, WindowPlacementInfo placement)
    {
        return MoveWindowToPlacement(hWnd, placement, null);
    }

    /// <summary>
    /// Repositions and resizes a window to the target placement across single- and multi-monitor setups.
    /// Reliably translates coordinates across monitors and handles maximized/minimized transitions.
    /// Correctly supports snapped windows (Aero Snap) using exact screen bounds.
    /// </summary>
    public static bool MoveWindowToPlacement(IntPtr hWnd, WindowPlacementInfo placement, WindowRect? bounds)
    {
        if (hWnd == IntPtr.Zero) return false;

        // Determine target position: for Normal state or Maximized monitor positioning,
        // bounds gives the true screen coordinates.
        var targetRect = (placement.State == WindowState.Normal && bounds != null && bounds.Width > 0 && bounds.Height > 0)
            ? bounds
            : (placement.NormalPosition != null && placement.NormalPosition.Width > 0 && placement.NormalPosition.Height > 0
                ? placement.NormalPosition
                : bounds ?? new WindowRect(100, 100, 900, 700));

        // 1. Check current window state
        var currentWp = NativeMethods.WINDOWPLACEMENT.Create();
        NativeMethods.GetWindowPlacement(hWnd, ref currentWp);

        // 2. If currently minimized or maximized, restore first so Windows allows repositioning
        if (currentWp.showCmd == NativeMethods.SW_SHOWMINIMIZED)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            System.Threading.Thread.Sleep(30);
        }
        else if (currentWp.showCmd == NativeMethods.SW_SHOWMAXIMIZED && placement.State != WindowState.Maximized)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            System.Threading.Thread.Sleep(20);
        }
        else if (currentWp.showCmd == NativeMethods.SW_SHOWMAXIMIZED && placement.State == WindowState.Maximized)
        {
            // If window is already maximized, check if it needs to move to another monitor
            NativeMethods.GetWindowRect(hWnd, out var curRect);
            int curCenterX = curRect.Left + (curRect.Right - curRect.Left) / 2;
            int targetCenterX = targetRect.Left + targetRect.Width / 2;
            if (Math.Abs(curCenterX - targetCenterX) > 300)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                System.Threading.Thread.Sleep(20);
            }
        }

        // 3. Move and size window to exact target coordinates on the target monitor
        NativeMethods.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            targetRect.Left,
            targetRect.Top,
            targetRect.Width,
            targetRect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
        );

        // 4. Apply target final state (Maximized, Minimized, Normal)
        if (placement.State == WindowState.Maximized)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWMAXIMIZED);
        }
        else if (placement.State == WindowState.Minimized)
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWMINIMIZED);
        }
        else
        {
            if (currentWp.showCmd != NativeMethods.SW_SHOWNORMAL)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNORMAL);
            }

            // Re-apply SetWindowPos to guarantee final coordinates on the target monitor
            NativeMethods.SetWindowPos(
                hWnd,
                IntPtr.Zero,
                targetRect.Left,
                targetRect.Top,
                targetRect.Width,
                targetRect.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW
            );
        }

        return true;
    }

    /// <summary>
    /// Retrieves all currently open, valid application windows.
    /// </summary>
    public List<WindowInfo> GetCurrentOpenWindows()
    {
        var list = new List<WindowInfo>();
        var shellHwnd = NativeMethods.GetShellWindow();
        int currentPid = Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (IsValidAppWindow(hWnd, shellHwnd, currentPid))
            {
                var info = ExtractWindowInfo(hWnd, currentPid);
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
    /// </summary>
    public static bool IsValidAppWindow(IntPtr hWnd, IntPtr shellHwnd, int currentPid = 0)
    {
        if (hWnd == IntPtr.Zero || hWnd == shellHwnd)
            return false;

        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Exclude our own application process
        if (currentPid != 0)
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);
            if (windowPid == currentPid)
                return false;
        }

        // Filter out cloaked windows (suspended UWP apps or windows on other virtual desktops)
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

        return true;
    }

    private WindowInfo? ExtractWindowInfo(IntPtr hWnd, int currentPid)
    {
        try
        {
            // Process & PID
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0 || processId == currentPid)
                return null;

            string processName = string.Empty;
            string? exePath = null;

            try
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;

                if (IgnoredProcesses.Contains(processName))
                    return null;

                exePath = GetProcessExecutablePath(processId, proc);
                var resolvedExe = WorkspaceSwitcher.Core.Services.AppIdentityHelper.ResolveLaunchableExecutable(processName, exePath);
                if (!string.IsNullOrWhiteSpace(resolvedExe))
                {
                    exePath = resolvedExe;
                }
            }
            catch
            {
                return null;
            }

            // Title
            var titleSb = new StringBuilder(512);
            NativeMethods.GetWindowText(hWnd, titleSb, titleSb.Capacity);
            string title = titleSb.ToString();

            // Class Name
            var classSb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, classSb, classSb.Capacity);
            string className = classSb.ToString();

            // Window Placement
            var wp = NativeMethods.WINDOWPLACEMENT.Create();
            if (!NativeMethods.GetWindowPlacement(hWnd, ref wp))
            {
                return null;
            }

            var placementInfo = WindowPlacementInfo.FromNative(wp);

            // Bounding Rect: If minimized, use normalPosition instead of -32000
            WindowRect bounds;
            if (placementInfo.State == WindowState.Minimized)
            {
                bounds = new WindowRect(
                    placementInfo.NormalPosition.Left,
                    placementInfo.NormalPosition.Top,
                    placementInfo.NormalPosition.Right,
                    placementInfo.NormalPosition.Bottom
                );
            }
            else
            {
                NativeMethods.GetWindowRect(hWnd, out var rect);
                bounds = WindowRect.FromNative(rect);
            }

            // In Windows 10/11, Aero Snap (snapping windows side-by-side or to corners) does NOT update 
            // WINDOWPLACEMENT.rcNormalPosition. Synchronize NormalPosition with actual screen bounds for normal windows.
            if (placementInfo.State == WindowState.Normal && bounds.Width > 0 && bounds.Height > 0)
            {
                placementInfo.NormalPosition = new WindowRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            }

            return new WindowInfo
            {
                Handle = hWnd,
                ProcessId = processId,
                ProcessName = processName,
                ExecutablePath = exePath,
                WindowTitle = title,
                ClassName = className,
                Placement = placementInfo,
                Bounds = bounds
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProcessExecutablePath(uint processId, Process proc)
    {
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

        // 1. Exact Match: ProcessMatch + WindowTitle + ClassName
        var match = candidates.FirstOrDefault(w =>
            WorkspaceSwitcher.Core.Services.AppIdentityHelper.IsMatchingProcess(w.ProcessName, savedWindow.ProcessName) &&
            string.Equals(w.WindowTitle, savedWindow.WindowTitle, StringComparison.Ordinal) &&
            string.Equals(w.ClassName, savedWindow.ClassName, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 2. Strong Match: ProcessMatch + WindowTitle
        match = candidates.FirstOrDefault(w =>
            WorkspaceSwitcher.Core.Services.AppIdentityHelper.IsMatchingProcess(w.ProcessName, savedWindow.ProcessName) &&
            string.Equals(w.WindowTitle, savedWindow.WindowTitle, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 3. Partial Match: ProcessMatch + ClassName
        match = candidates.FirstOrDefault(w =>
            WorkspaceSwitcher.Core.Services.AppIdentityHelper.IsMatchingProcess(w.ProcessName, savedWindow.ProcessName) &&
            string.Equals(w.ClassName, savedWindow.ClassName, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 4. Fallback: First available window of matching process
        match = candidates.FirstOrDefault(w =>
            WorkspaceSwitcher.Core.Services.AppIdentityHelper.IsMatchingProcess(w.ProcessName, savedWindow.ProcessName));

        return match;
    }
}
