using System;
using System.Collections.Generic;

namespace WorkspaceSwitcher.Core.Models;

/// <summary>
/// Represents the saved Windows taskbar pinned applications state for a workspace.
/// </summary>
public class TaskbarConfiguration
{
    public bool Enabled { get; set; } = true;
    public byte[]? Favorites { get; set; }
    public byte[]? FavoritesResolve { get; set; }
    public int? FavoritesVersion { get; set; }
    public int? FavoritesChanges { get; set; }
    public List<TaskbarPinnedItem> PinnedItems { get; set; } = new();
    public DateTime? CapturedAt { get; set; } = DateTime.UtcNow;

    public TaskbarConfiguration() { }
}
