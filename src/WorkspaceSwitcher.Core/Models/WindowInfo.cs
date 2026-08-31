using System;
using System.Text.Json.Serialization;

namespace WorkspaceSwitcher.Core.Models;

public class WindowInfo
{
    /// <summary>
    /// Name of the running executable process (e.g. "devenv", "code", "chrome").
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the executable file on disk (used to optionally launch apps if closed).
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Window title bar text at the time the snapshot was captured.
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// Win32 window class name (e.g. "Chrome_WidgetWin_1", "Notepad").
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Full window placement information including minimized/maximized state and restored bounds.
    /// </summary>
    public WindowPlacementInfo Placement { get; set; } = new();

    /// <summary>
    /// Actual visible bounding rectangle.
    /// </summary>
    public WindowRect Bounds { get; set; } = new();

    /// <summary>
    /// Runtime window handle (HWND). Ignored during JSON serialization.
    /// </summary>
    [JsonIgnore]
    public IntPtr Handle { get; set; }

    /// <summary>
    /// Process ID at runtime. Ignored during JSON serialization.
    /// </summary>
    [JsonIgnore]
    public uint ProcessId { get; set; }

    public override string ToString()
    {
        return $"[{ProcessName}] \"{WindowTitle}\" ({Bounds.Width}x{Bounds.Height} at {Bounds.Left},{Bounds.Top})";
    }
}
