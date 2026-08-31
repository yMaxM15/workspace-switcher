using System;
using System.Text.Json;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Models;

Console.WriteLine("=== Workspace / Window Layout Switcher (Core Engine Test) ===");

var windowManager = new WindowManager();

Console.WriteLine("\n[1] Capturing active application windows...");
var profile = windowManager.CaptureWorkspace("Default_Test_Profile", "Snapshot taken from CLI test tool");

Console.WriteLine($"Captured {profile.Windows.Count} active application windows:\n");

foreach (var win in profile.Windows)
{
    Console.WriteLine($"• [{win.ProcessName}] \"{win.WindowTitle}\"");
    Console.WriteLine($"   Path:  {win.ExecutablePath ?? "N/A"}");
    Console.WriteLine($"   Class: {win.ClassName}");
    Console.WriteLine($"   State: {win.Placement.State}");
    Console.WriteLine($"   Bounds (Normal): X={win.Placement.NormalPosition.Left}, Y={win.Placement.NormalPosition.Top}, " +
                      $"W={win.Placement.NormalPosition.Width}, H={win.Placement.NormalPosition.Height}");
    Console.WriteLine();
}

var options = new JsonSerializerOptions { WriteIndented = true };
string json = JsonSerializer.Serialize(profile, options);
Console.WriteLine("--- JSON Preview ---");
Console.WriteLine(json);
