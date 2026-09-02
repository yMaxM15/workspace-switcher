using System;
using System.Threading;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.Core.Services;

Console.WriteLine("=================================================");
Console.WriteLine("  Workspace / Window Layout Switcher Engine v1.0");
Console.WriteLine("=================================================");

var windowManager = new WindowManager();
var profileService = new ProfileService();
var settingsService = new SettingsService();
var taskbarService = new TaskbarService();

if (args.Length > 0 && args[0] == "apply-taskbar")
{
    string profileName = args.Length > 1 ? args[1] : "Coding";
    Console.WriteLine($"[TEST] Applying taskbar for profile: {profileName}");
    var prof = profileService.LoadProfile(profileName);
    if (prof?.Taskbar == null)
    {
        Console.WriteLine("[ERROR] Profile has no taskbar configuration!");
        return;
    }

    bool success = taskbarService.ApplyTaskbar(prof.Taskbar);
    Console.WriteLine($"[RESULT] ApplyTaskbar returned: {success}");
    return;
}

if (args.Length > 0 && args[0] == "snapshot-taskbar")
{
    string profileName = args.Length > 1 ? args[1] : "test";
    Console.WriteLine($"[SNAPSHOT] Capturing taskbar for profile: {profileName}");
    var prof = profileService.LoadProfile(profileName);
    if (prof != null)
    {
        prof.Taskbar = taskbarService.CaptureCurrentTaskbar();
        profileService.SaveProfile(prof);
        Console.WriteLine($"[SNAPSHOT] Saved {prof.Taskbar.PinnedItems.Count} taskbar pin(s) to '{prof.Name}'.");
    }
    return;
}

Console.WriteLine($"\n[INFO] Profiles Directory: {profileService.ProfilesDirectory}");

// 1. Capture snapshot
Console.WriteLine("\n[1] Capturing snapshot 'Coding' profile...");
var codingProfile = windowManager.CaptureWorkspace("Coding", "Dual monitor development setup");
profileService.SaveProfile(codingProfile);
int pinCount = codingProfile.Taskbar?.PinnedItems.Count ?? 0;
Console.WriteLine($"Saved '{codingProfile.Name}' with {codingProfile.Windows.Count} windows and {pinCount} taskbar pins to JSON.");
if (codingProfile.Taskbar?.PinnedItems != null)
{
    foreach (var item in codingProfile.Taskbar.PinnedItems)
    {
        Console.WriteLine($"   📌 {item.DisplayName} (File: {item.ShortcutFileName}, Static: {item.IsStatic})");
    }
}

// 2. List all profiles
Console.WriteLine("\n[2] Existing profiles on disk:");
var profiles = profileService.GetAllProfiles();
foreach (var p in profiles)
{
    int pPins = p.Taskbar?.PinnedItems.Count ?? 0;
    Console.WriteLine($" • [{p.Name}] - {p.Windows.Count} windows, {pPins} taskbar pins (Created: {p.CreatedAt:yyyy-MM-dd HH:mm:ss})");
}

// 3. Setup Global Hotkey Demo
Console.WriteLine("\n[3] Initializing Global Hotkey Manager...");
using var hotkeyManager = new HotkeyManager();

hotkeyManager.HotKeyPressed += (sender, e) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n>>> HOTKEY DETECTED! ID: {e.HotKeyId}, Modifiers: {e.Modifiers}, VirtualKey: {e.VirtualKey}");
    if (e.Binding != null)
    {
        Console.WriteLine($"    Action: {e.Binding.Action} for Profile: '{e.Binding.TargetProfileName}'");
        if (e.Binding.Action == HotKeyAction.RestoreProfile)
        {
            var target = profileService.LoadProfile(e.Binding.TargetProfileName);
            if (target != null)
            {
                int restored = windowManager.RestoreWorkspace(target, launchIfNotRunning: false);
                Console.WriteLine($"    Restored {restored} window positions successfully!");
            }
        }
    }
    Console.ResetColor();
};

try
{
    // Register Ctrl+Alt+1 (VirtualKey 0x31 for '1')
    int id1 = hotkeyManager.Register(KeyModifiers.Control | KeyModifiers.Alt, 0x31, "Coding", HotKeyAction.RestoreProfile);
    Console.WriteLine($"[HOTKEY REGISTERED] Ctrl + Alt + 1 -> Restore 'Coding' (ID: {id1})");

    // Register Ctrl+Alt+S (VirtualKey 0x53 for 'S')
    int id2 = hotkeyManager.Register(KeyModifiers.Control | KeyModifiers.Alt, 0x53, "Coding", HotKeyAction.CaptureProfile);
    Console.WriteLine($"[HOTKEY REGISTERED] Ctrl + Alt + S -> Snapshot 'Coding' (ID: {id2})");

    Console.WriteLine("\n[LISTENING] Global hotkeys are active! Press Ctrl+Alt+1 or Ctrl+Alt+S anywhere in Windows.");
    Console.WriteLine("Press [Q] or [Ctrl+C] to exit demo.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Warning registering demo hotkeys: {ex.Message}");
    Console.ResetColor();
}

if (!Console.IsInputRedirected)
{
    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q) break;
        }
        Thread.Sleep(100);
    }
}
else
{
    Console.WriteLine("\n[INFO] Running in non-interactive mode. Press Ctrl+C to terminate.");
    Thread.Sleep(2000);
}

Console.WriteLine("\nShutting down HotkeyManager...");
