using System.Collections.Generic;
using WorkspaceSwitcher.Core.Hotkeys;

namespace WorkspaceSwitcher.Core.Models;

public class AppSettings
{
    public bool AutoLaunchMissingApps { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string? CustomProfilesDirectory { get; set; }
    public List<HotKeyBinding> Hotkeys { get; set; } = new();
}
