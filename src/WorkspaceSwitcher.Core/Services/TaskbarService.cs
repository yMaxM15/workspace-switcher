using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.Core.Native;

namespace WorkspaceSwitcher.Core.Services;

/// <summary>
/// Service for snapshotting, restoring, and managing Windows taskbar pinned applications per workspace.
/// </summary>
public class TaskbarService
{
    public static readonly string TaskbarShortcutsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
    );

    public const string TaskbandRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";

    /// <summary>
    /// Captures the current Windows taskbar pinned applications and registry layout.
    /// </summary>
    public TaskbarConfiguration CaptureCurrentTaskbar(IEnumerable<string>? staticAppIdentifiers = null)
    {
        var config = new TaskbarConfiguration
        {
            Enabled = true,
            CapturedAt = DateTime.UtcNow,
            PinnedItems = new List<TaskbarPinnedItem>()
        };

        var staticSet = new HashSet<string>(staticAppIdentifiers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // 1. Snapshot shortcut (.lnk) files from the User Pinned directory
        if (Directory.Exists(TaskbarShortcutsDirectory))
        {
            var directoryInfo = new DirectoryInfo(TaskbarShortcutsDirectory);
            foreach (var file in directoryInfo.GetFiles("*.lnk"))
            {
                var item = new TaskbarPinnedItem
                {
                    ShortcutFileName = file.Name,
                    DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                    IsStatic = staticSet.Contains(file.Name) || staticSet.Contains(Path.GetFileNameWithoutExtension(file.Name))
                };

                try
                {
                    item.Base64Data = Convert.ToBase64String(File.ReadAllBytes(file.FullName));
                }
                catch { }

                try
                {
                    var (targetPath, args) = ResolveShortcut(file.FullName);
                    item.TargetPath = targetPath;
                    item.Arguments = args;
                }
                catch { }

                config.PinnedItems.Add(item);
            }
        }

        // 2. Snapshot HKCU Explorer Taskband registry state (order, resolve items, version)
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TaskbandRegistryKey);
            if (key != null)
            {
                if (key.GetValue("Favorites") is byte[] fav) config.Favorites = fav;
                if (key.GetValue("FavoritesResolve") is byte[] res) config.FavoritesResolve = res;
                if (key.GetValue("FavoritesChanges") is int chg) config.FavoritesChanges = chg;
                if (key.GetValue("FavoritesVersion") is int ver) config.FavoritesVersion = ver;
            }
        }
        catch { }

        return config;
    }

    /// <summary>
    /// Restores a saved taskbar configuration and restarts Explorer so the taskbar updates immediately.
    /// </summary>
    public bool ApplyTaskbar(TaskbarConfiguration config, IEnumerable<string>? staticAppIdentifiers = null)
    {
        if (config == null || !config.Enabled) return false;

        try
        {
            // 1. Ensure the user pinned directory exists
            if (!Directory.Exists(TaskbarShortcutsDirectory))
            {
                Directory.CreateDirectory(TaskbarShortcutsDirectory);
            }

            var staticSet = new HashSet<string>(staticAppIdentifiers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var activeShortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2. Restore shortcut files from the profile
            foreach (var item in config.PinnedItems)
            {
                activeShortcuts.Add(item.ShortcutFileName);
                string targetPath = Path.Combine(TaskbarShortcutsDirectory, item.ShortcutFileName);

                if (!string.IsNullOrWhiteSpace(item.Base64Data))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(item.Base64Data);
                        File.WriteAllBytes(targetPath, bytes);
                    }
                    catch { }
                }
                else if (!string.IsNullOrWhiteSpace(item.TargetPath))
                {
                    CreateShortcut(targetPath, item.TargetPath, item.Arguments);
                }
            }

            // 3. Clean up non-static shortcuts not in this workspace layout
            var dir = new DirectoryInfo(TaskbarShortcutsDirectory);
            foreach (var file in dir.GetFiles("*.lnk"))
            {
                bool isWorkspaceItem = activeShortcuts.Contains(file.Name);
                bool isStaticItem = staticSet.Contains(file.Name) || staticSet.Contains(Path.GetFileNameWithoutExtension(file.Name));

                if (!isWorkspaceItem && !isStaticItem)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch { }
                }
            }

            // 4. Restore Taskband registry values
            using (var key = Registry.CurrentUser.CreateSubKey(TaskbandRegistryKey, writable: true))
            {
                if (key != null)
                {
                    if (config.Favorites != null && config.Favorites.Length > 0)
                        key.SetValue("Favorites", config.Favorites, RegistryValueKind.Binary);
                    if (config.FavoritesResolve != null && config.FavoritesResolve.Length > 0)
                        key.SetValue("FavoritesResolve", config.FavoritesResolve, RegistryValueKind.Binary);
                    if (config.FavoritesChanges.HasValue)
                        key.SetValue("FavoritesChanges", config.FavoritesChanges.Value, RegistryValueKind.DWord);
                    if (config.FavoritesVersion.HasValue)
                        key.SetValue("FavoritesVersion", config.FavoritesVersion.Value, RegistryValueKind.DWord);
                }
            }

            // 5. Restart Explorer shell cleanly so the taskbar loads the new layout
            RestartExplorer();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskbarService] Error applying taskbar: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restarts the Windows Explorer shell cleanly without opening extra folder windows.
    /// </summary>
    public void RestartExplorer()
    {
        try
        {
            var explorers = Process.GetProcessesByName("explorer");
            foreach (var p in explorers)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }
                catch { }
            }

            // Wait briefly for Windows to auto-restart the shell (standard behavior on Windows 10/11)
            Thread.Sleep(800);

            // If Windows did not auto-restart explorer within the window, launch it explicitly
            if (Process.GetProcessesByName("explorer").Length == 0)
            {
                string explorerExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = explorerExe,
                    UseShellExecute = true
                });
                Thread.Sleep(800);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskbarService] Error restarting explorer: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves target path and arguments from a .lnk shortcut file using native IShellLink COM interface.
    /// </summary>
    public static (string? TargetPath, string? Arguments) ResolveShortcut(string shortcutPath)
    {
        if (!File.Exists(shortcutPath)) return (null, null);

        try
        {
            var link = (NativeMethods.IShellLinkW)new NativeMethods.ShellLink();
            ((NativeMethods.IPersistFile)link).Load(shortcutPath, 0);

            var pathSb = new StringBuilder(260);
            link.GetPath(pathSb, pathSb.Capacity, out _, 0);

            var argsSb = new StringBuilder(1024);
            link.GetArguments(argsSb, argsSb.Capacity);

            return (pathSb.ToString(), argsSb.ToString());
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Creates or updates a .lnk shortcut file with target executable and arguments.
    /// </summary>
    public static bool CreateShortcut(string shortcutPath, string targetPath, string? arguments = null)
    {
        try
        {
            var link = (NativeMethods.IShellLinkW)new NativeMethods.ShellLink();
            link.SetPath(targetPath);
            if (!string.IsNullOrEmpty(arguments))
            {
                link.SetArguments(arguments);
            }

            string? workingDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(workingDir))
            {
                link.SetWorkingDirectory(workingDir);
            }

            ((NativeMethods.IPersistFile)link).Save(shortcutPath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Synchronizes a pinned item designated as Static across all workspace profiles.
    /// </summary>
    public void SyncStaticItemAcrossProfiles(
        TaskbarPinnedItem item, 
        bool isStatic, 
        IEnumerable<WorkspaceProfile> profiles, 
        Action<WorkspaceProfile> saveProfile)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(saveProfile);

        foreach (var profile in profiles)
        {
            if (profile.Taskbar == null) continue;

            var existing = profile.Taskbar.PinnedItems.FirstOrDefault(p =>
                string.Equals(p.ShortcutFileName, item.ShortcutFileName, StringComparison.OrdinalIgnoreCase));

            if (isStatic)
            {
                if (existing != null)
                {
                    existing.IsStatic = true;
                }
                else
                {
                    profile.Taskbar.PinnedItems.Add(new TaskbarPinnedItem
                    {
                        ShortcutFileName = item.ShortcutFileName,
                        DisplayName = item.DisplayName,
                        TargetPath = item.TargetPath,
                        Arguments = item.Arguments,
                        Base64Data = item.Base64Data,
                        IsStatic = true
                    });
                }
                saveProfile(profile);
            }
            else
            {
                if (existing != null)
                {
                    existing.IsStatic = false;
                    saveProfile(profile);
                }
            }
        }
    }
}
