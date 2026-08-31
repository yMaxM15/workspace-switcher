using System;
using System.Collections.Generic;

namespace WorkspaceSwitcher.Core.Hotkeys;

public static class HotkeyHelper
{
    public static readonly IReadOnlyList<string> AvailableModifiers = new[]
    {
        "Ctrl + Alt",
        "Ctrl + Shift",
        "Alt + Shift",
        "Win + Alt",
        "Ctrl + Win",
        "None"
    };

    public static readonly IReadOnlyList<string> AvailableKeys = new[]
    {
        "Auto (1-5)",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "None (Disabled)"
    };

    public static KeyModifiers ParseModifiers(string? modifierStr)
    {
        if (string.IsNullOrWhiteSpace(modifierStr))
            return KeyModifiers.Control | KeyModifiers.Alt;

        var mods = KeyModifiers.None;
        if (modifierStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase))
            mods |= KeyModifiers.Control;
        if (modifierStr.Contains("Alt", StringComparison.OrdinalIgnoreCase))
            mods |= KeyModifiers.Alt;
        if (modifierStr.Contains("Shift", StringComparison.OrdinalIgnoreCase))
            mods |= KeyModifiers.Shift;
        if (modifierStr.Contains("Win", StringComparison.OrdinalIgnoreCase))
            mods |= KeyModifiers.Win;

        return mods;
    }

    public static uint ParseVirtualKey(string? keyStr, int defaultIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(keyStr) || keyStr.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (defaultIndex >= 0 && defaultIndex < 9)
            {
                return (uint)(0x31 + defaultIndex); // '1'..'9'
            }
            return 0x31;
        }

        if (keyStr.StartsWith("None", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string upper = keyStr.Trim().ToUpperInvariant();

        // Numbers 0-9
        if (upper.Length == 1 && char.IsDigit(upper[0]))
        {
            return (uint)upper[0];
        }

        // Letters A-Z
        if (upper.Length == 1 && upper[0] >= 'A' && upper[0] <= 'Z')
        {
            return (uint)upper[0];
        }

        // Function keys F1-F12
        if (upper.StartsWith("F") && int.TryParse(upper[1..], out int fNum) && fNum >= 1 && fNum <= 12)
        {
            return (uint)(0x70 + (fNum - 1));
        }

        return 0;
    }

    public static string FormatDisplayHotkey(string? modifier, string? key, int index = 0)
    {
        if (string.Equals(key, "None (Disabled)", StringComparison.OrdinalIgnoreCase))
            return "No Hotkey";

        string mod = string.IsNullOrWhiteSpace(modifier) ? "Ctrl + Alt" : modifier;
        if (string.Equals(mod, "None", StringComparison.OrdinalIgnoreCase))
            mod = "";

        string k = key ?? "";
        if (string.IsNullOrWhiteSpace(k) || k.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
        {
            k = (index + 1 <= 5) ? (index + 1).ToString() : "";
        }

        if (string.IsNullOrWhiteSpace(k)) return "No Hotkey";
        return string.IsNullOrWhiteSpace(mod) ? k : $"{mod} + {k}";
    }
}
