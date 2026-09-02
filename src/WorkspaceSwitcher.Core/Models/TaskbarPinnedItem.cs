using System;

namespace WorkspaceSwitcher.Core.Models;

/// <summary>
/// Represents a pinned application on the Windows taskbar.
/// </summary>
public class TaskbarPinnedItem
{
    public string ShortcutFileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? TargetPath { get; set; }
    public string? Arguments { get; set; }
    public string? Base64Data { get; set; }
    public bool IsStatic { get; set; }

    public TaskbarPinnedItem() { }

    public TaskbarPinnedItem(string shortcutFileName, string displayName, string? targetPath = null, bool isStatic = false)
    {
        ShortcutFileName = shortcutFileName;
        DisplayName = displayName;
        TargetPath = targetPath;
        IsStatic = isStatic;
    }
}
