using System.Text.Json.Serialization;
using WorkspaceSwitcher.Core.Native;

namespace WorkspaceSwitcher.Core.Models;

public enum WindowState
{
    Normal = 1,
    Minimized = 2,
    Maximized = 3
}

public class WindowRect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }

    [JsonIgnore]
    public int Width => Right - Left;

    [JsonIgnore]
    public int Height => Bottom - Top;

    public WindowRect() { }

    public WindowRect(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static WindowRect FromNative(NativeMethods.RECT rect)
    {
        return new WindowRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public NativeMethods.RECT ToNative()
    {
        return new NativeMethods.RECT(Left, Top, Right, Bottom);
    }
}

public class WindowPoint
{
    public int X { get; set; }
    public int Y { get; set; }

    public WindowPoint() { }

    public WindowPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static WindowPoint FromNative(NativeMethods.POINT point)
    {
        return new WindowPoint(point.X, point.Y);
    }

    public NativeMethods.POINT ToNative()
    {
        return new NativeMethods.POINT(X, Y);
    }
}

public class WindowPlacementInfo
{
    public int Flags { get; set; }
    public WindowState State { get; set; } = WindowState.Normal;
    public WindowPoint MinPosition { get; set; } = new();
    public WindowPoint MaxPosition { get; set; } = new();
    public WindowRect NormalPosition { get; set; } = new();

    public static WindowPlacementInfo FromNative(NativeMethods.WINDOWPLACEMENT wp)
    {
        var state = wp.showCmd switch
        {
            NativeMethods.SW_SHOWMAXIMIZED => WindowState.Maximized,
            NativeMethods.SW_SHOWMINIMIZED => WindowState.Minimized,
            _ => WindowState.Normal
        };

        return new WindowPlacementInfo
        {
            Flags = wp.flags,
            State = state,
            MinPosition = WindowPoint.FromNative(wp.ptMinPosition),
            MaxPosition = WindowPoint.FromNative(wp.ptMaxPosition),
            NormalPosition = WindowRect.FromNative(wp.rcNormalPosition)
        };
    }

    public NativeMethods.WINDOWPLACEMENT ToNative()
    {
        var wp = NativeMethods.WINDOWPLACEMENT.Create();
        wp.flags = Flags;
        wp.showCmd = State switch
        {
            WindowState.Maximized => NativeMethods.SW_SHOWMAXIMIZED,
            WindowState.Minimized => NativeMethods.SW_SHOWMINIMIZED,
            _ => NativeMethods.SW_SHOWNORMAL
        };
        wp.ptMinPosition = MinPosition.ToNative();
        wp.ptMaxPosition = MaxPosition.ToNative();
        wp.rcNormalPosition = NormalPosition.ToNative();
        return wp;
    }
}
