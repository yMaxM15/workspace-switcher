using System;
using System.Collections.Generic;

namespace WorkspaceSwitcher.Core.Models;

public class WorkspaceProfile
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    public int DisplayCount { get; set; } = 1;
    public List<WindowInfo> Windows { get; set; } = new();

    public WorkspaceProfile() { }

    public WorkspaceProfile(string name)
    {
        Name = name;
        CreatedAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
    }
}
