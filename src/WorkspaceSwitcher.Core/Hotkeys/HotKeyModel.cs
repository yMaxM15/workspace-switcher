using System;

namespace WorkspaceSwitcher.Core.Hotkeys;

public enum HotKeyAction
{
    RestoreProfile = 0,
    CaptureProfile = 1
}

public class HotKeyBinding
{
    public int Id { get; set; }
    public string TargetProfileName { get; set; } = string.Empty;
    public KeyModifiers Modifiers { get; set; } = KeyModifiers.Control | KeyModifiers.Alt;
    public uint VirtualKey { get; set; }
    public HotKeyAction Action { get; set; } = HotKeyAction.RestoreProfile;
    public string KeyDisplayString { get; set; } = string.Empty;

    public override string ToString()
    {
        var modStr = Modifiers.ToString().Replace(", ", "+");
        return $"{modStr}+{(char)VirtualKey} -> {Action}: {TargetProfileName}";
    }
}

public class HotKeyEventArgs : EventArgs
{
    public int HotKeyId { get; }
    public KeyModifiers Modifiers { get; }
    public uint VirtualKey { get; }
    public HotKeyBinding? Binding { get; }

    public HotKeyEventArgs(int id, KeyModifiers modifiers, uint vk, HotKeyBinding? binding = null)
    {
        HotKeyId = id;
        Modifiers = modifiers;
        VirtualKey = vk;
        Binding = binding;
    }
}
