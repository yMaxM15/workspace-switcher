using System.Windows.Media;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.UI.Services;

namespace WorkspaceSwitcher.UI.ViewModels;

public class WindowItemViewModel
{
    public WindowInfo Model { get; }
    public string ProcessName => Model.ProcessName;
    public string WindowTitle => string.IsNullOrWhiteSpace(Model.WindowTitle) ? Model.ProcessName : Model.WindowTitle;
    public string ExecutablePath => Model.ExecutablePath ?? "System / UWP Process";
    public WindowState State => Model.Placement.State;

    public string StateText => State.ToString();

    public string MonitorDisplay
    {
        get
        {
            int left = Model.Placement.NormalPosition.Left;
            if (left < 0) return "Monitor 2";
            if (left >= 1920) return "Monitor 3";
            return "Monitor 1";
        }
    }

    public string FormattedCoordinates
    {
        get
        {
            var p = Model.Placement.NormalPosition;
            return $"{p.Left}, {p.Top} • {p.Width} × {p.Height}";
        }
    }

    public ImageSource? AppIcon { get; }

    public WindowItemViewModel(WindowInfo windowInfo)
    {
        Model = windowInfo;
        AppIcon = IconHelper.GetIconForExecutable(windowInfo.ExecutablePath, windowInfo.ProcessName);
    }
}
