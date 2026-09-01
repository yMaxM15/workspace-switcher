using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WorkspaceSwitcher.Core.Services;

public static class AppIdentityHelper
{
    private static readonly Dictionary<string, string> KnownAppNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ts3client_win64", "TeamSpeak 3" },
        { "ts3client_win32", "TeamSpeak 3" },
        { "ts3", "TeamSpeak 3" },
        { "teamspeak", "TeamSpeak 3" },
        { "steamwebhelper", "Steam" },
        { "steam", "Steam" },
        { "LeagueClientUx", "League of Legends" },
        { "LeagueClient", "League of Legends" },
        { "Riot Client", "Riot Client" },
        { "WindowsTerminal", "Windows Terminal" },
        { "devenv", "Visual Studio" },
        { "code", "Visual Studio Code" },
        { "firefox", "Mozilla Firefox" },
        { "chrome", "Google Chrome" },
        { "msedge", "Microsoft Edge" },
        { "explorer", "File Explorer" },
        { "discord", "Discord" },
        { "spotify", "Spotify" },
        { "taskmgr", "Task Manager" },
        { "notepad", "Notepad" },
        { "powershell", "PowerShell" },
        { "cmd", "Command Prompt" }
    };

    /// <summary>
    /// Gets a friendly, human-readable display name for an application.
    /// Prefers known mappings, then file metadata (FileDescription / ProductName),
    /// and falls back to a clean version of the process name.
    /// </summary>
    public static string GetFriendlyName(string processName, string? executablePath = null, string? windowTitle = null)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return "Application";

        if (KnownAppNames.TryGetValue(processName, out var friendly))
        {
            return friendly;
        }

        // Try extracting from executable metadata if available
        if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(executablePath);
                if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                {
                    return vi.FileDescription.Trim();
                }
                if (!string.IsNullOrWhiteSpace(vi.ProductName))
                {
                    return vi.ProductName.Trim();
                }
            }
            catch
            {
                // Fallback to heuristic
            }
        }

        // Heuristic fallback: clean process name
        return CleanProcessName(processName);
    }

    /// <summary>
    /// Cleans common suffixes like _win64, _x64, etc. from technical process names.
    /// e.g. "ts3client_win64" -> "ts3"
    /// </summary>
    public static string CleanProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return string.Empty;

        var clean = processName;
        string[] suffixes = { "_win64", "_win32", "_x64", "_x86", "client_win64", "client_win32" };
        foreach (var s in suffixes)
        {
            if (clean.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - s.Length);
                break;
            }
        }

        if (clean.EndsWith("Client", StringComparison.OrdinalIgnoreCase) && clean.Length > 6)
        {
            clean = clean.Substring(0, clean.Length - 6);
        }

        return string.IsNullOrWhiteSpace(clean) ? processName : clean;
    }

    /// <summary>
    /// Checks whether two process names refer to the same application (e.g. steam and steamwebhelper).
    /// </summary>
    public static bool IsMatchingProcess(string proc1, string proc2)
    {
        if (string.Equals(proc1, proc2, StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsSteam(proc1) && IsSteam(proc2))
            return true;

        if (IsTeamSpeak(proc1) && IsTeamSpeak(proc2))
            return true;

        if (IsPowerShell(proc1) && IsPowerShell(proc2))
            return true;

        if (IsTerminal(proc1) && IsTerminal(proc2))
            return true;

        return false;
    }

    public static bool IsSteam(string processName) =>
        processName.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase);

    public static bool IsTeamSpeak(string processName) =>
        processName.StartsWith("ts3", StringComparison.OrdinalIgnoreCase) ||
        processName.StartsWith("teamspeak", StringComparison.OrdinalIgnoreCase);

    public static bool IsPowerShell(string processName) =>
        processName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("pwsh", StringComparison.OrdinalIgnoreCase);

    public static bool IsTerminal(string processName) =>
        processName.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("wt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the actual launchable executable path for an application.
    /// For example, resolves steamwebhelper.exe to steam.exe, or WindowsTerminal to wt.exe.
    /// </summary>
    public static string? ResolveLaunchableExecutable(string processName, string? executablePath)
    {
        if (IsSteam(processName) || (!string.IsNullOrWhiteSpace(executablePath) && executablePath.Contains("steamwebhelper", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(executablePath);
                    while (!string.IsNullOrEmpty(dir))
                    {
                        var candidate = Path.Combine(dir, "steam.exe");
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                        var parent = Directory.GetParent(dir);
                        if (parent == null || parent.FullName == dir) break;
                        dir = parent.FullName;
                    }
                }
                catch
                {
                    // Fallback to default
                }
            }

            // Fallback default Steam locations
            string defaultSteam64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
            if (File.Exists(defaultSteam64)) return defaultSteam64;

            string defaultSteam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe");
            if (File.Exists(defaultSteam)) return defaultSteam;
        }

        if (IsTerminal(processName))
        {
            string wtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(wtPath)) return wtPath;

            return "wt.exe";
        }

        if (processName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
        {
            string psPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(psPath)) return psPath;
        }

        if (processName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pwshPath = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwshPath)) return pwshPath;
            return "pwsh.exe";
        }

        if (processName.Equals("cmd", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        }

        if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        }

        if (processName.Equals("firefox", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath)) return executablePath;
            string defaultFf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "firefox.exe");
            if (File.Exists(defaultFf)) return defaultFf;
            string defaultFf86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", "firefox.exe");
            if (File.Exists(defaultFf86)) return defaultFf86;
        }

        if (processName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath)) return executablePath;
            string defaultChrome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe");
            if (File.Exists(defaultChrome)) return defaultChrome;
            string defaultChrome86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe");
            if (File.Exists(defaultChrome86)) return defaultChrome86;
        }

        if (processName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath)) return executablePath;
            string defaultEdge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");
            if (File.Exists(defaultEdge)) return defaultEdge;
            string defaultEdge64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe");
            if (File.Exists(defaultEdge64)) return defaultEdge64;
        }

        if (!string.IsNullOrWhiteSpace(executablePath) && executablePath.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
        {
            string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", $"{processName}.exe");
            if (File.Exists(alias)) return alias;
        }

        return executablePath;
    }
}
