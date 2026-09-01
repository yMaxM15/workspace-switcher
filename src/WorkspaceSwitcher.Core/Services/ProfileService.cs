using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WorkspaceSwitcher.Core.Models;

namespace WorkspaceSwitcher.Core.Services;

public class ProfileService : IProfileService
{
    private readonly string _profilesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ProfilesDirectory => _profilesDirectory;

    public ProfileService(string? customDirectory = null)
    {
        _profilesDirectory = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkspaceSwitcher",
            "Profiles"
        );

        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public void SaveProfile(WorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(profile));

        profile.LastModifiedAt = DateTime.UtcNow;
        string filePath = GetProfileFilePath(profile.Name);
        string tempPath = filePath + ".tmp";

        string json = JsonSerializer.Serialize(profile, _jsonOptions);

        // Safe atomic write pattern
        File.WriteAllText(tempPath, json);
        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }

    public async Task SaveProfileAsync(WorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(profile));

        profile.LastModifiedAt = DateTime.UtcNow;
        string filePath = GetProfileFilePath(profile.Name);
        string tempPath = filePath + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, profile, _jsonOptions);
        }

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }

    public WorkspaceProfile? LoadProfile(string profileName)
    {
        string filePath = GetProfileFilePath(profileName);
        if (!File.Exists(filePath))
            return null;

        string json = File.ReadAllText(filePath);
        var profile = JsonSerializer.Deserialize<WorkspaceProfile>(json, _jsonOptions);
        if (profile?.Windows != null)
        {
            profile.Windows.RemoveAll(w => string.Equals(w.ProcessName, "WorkspaceSwitcher.UI", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(w.ProcessName, "WorkspaceSwitcher", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(w.ProcessName, "WorkspaceSwitcher.Cli", StringComparison.OrdinalIgnoreCase));

            NormalizeProfileWindows(profile);
        }
        return profile;
    }

    public async Task<WorkspaceProfile?> LoadProfileAsync(string profileName)
    {
        string filePath = GetProfileFilePath(profileName);
        if (!File.Exists(filePath))
            return null;

        await using var stream = File.OpenRead(filePath);
        var profile = await JsonSerializer.DeserializeAsync<WorkspaceProfile>(stream, _jsonOptions);
        if (profile?.Windows != null)
        {
            profile.Windows.RemoveAll(w => string.Equals(w.ProcessName, "WorkspaceSwitcher.UI", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(w.ProcessName, "WorkspaceSwitcher", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(w.ProcessName, "WorkspaceSwitcher.Cli", StringComparison.OrdinalIgnoreCase));

            NormalizeProfileWindows(profile);
        }
        return profile;
    }

    /// <summary>
    /// Ensures that windows captured while snapped (Aero Snap) have their true visible bounds
    /// synced to Placement.NormalPosition, fixing Windows' Win32 WINDOWPLACEMENT rcNormalPosition desync.
    /// </summary>
    private static void NormalizeProfileWindows(WorkspaceProfile profile)
    {
        if (profile.Windows == null) return;

        foreach (var w in profile.Windows)
        {
            if (w.Placement == null)
            {
                w.Placement = new WindowPlacementInfo();
            }

            if (w.Bounds != null && w.Bounds.Width > 0 && w.Bounds.Height > 0)
            {
                if (w.Placement.State == WindowState.Normal)
                {
                    w.Placement.NormalPosition = new WindowRect(w.Bounds.Left, w.Bounds.Top, w.Bounds.Right, w.Bounds.Bottom);
                }
            }
            else if (w.Placement.NormalPosition != null && w.Placement.NormalPosition.Width > 0 && w.Placement.NormalPosition.Height > 0)
            {
                w.Bounds = new WindowRect(w.Placement.NormalPosition.Left, w.Placement.NormalPosition.Top, w.Placement.NormalPosition.Right, w.Placement.NormalPosition.Bottom);
            }
        }
    }

    public IReadOnlyList<string> GetProfileNames()
    {
        if (!Directory.Exists(_profilesDirectory))
            return Array.Empty<string>();

        var files = Directory.GetFiles(_profilesDirectory, "*.json");
        var names = new List<string>(files.Length);
        foreach (var file in files)
        {
            names.Add(Path.GetFileNameWithoutExtension(file));
        }
        return names;
    }

    public IReadOnlyList<WorkspaceProfile> GetAllProfiles()
    {
        var names = GetProfileNames();
        var profiles = new List<WorkspaceProfile>(names.Count);

        foreach (var name in names)
        {
            try
            {
                var profile = LoadProfile(name);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Skip corrupted or unreadable profile files
            }
        }

        return profiles;
    }

    public async Task<IReadOnlyList<WorkspaceProfile>> GetAllProfilesAsync()
    {
        var names = GetProfileNames();
        var profiles = new List<WorkspaceProfile>(names.Count);

        foreach (var name in names)
        {
            try
            {
                var profile = await LoadProfileAsync(name);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Skip corrupted or unreadable profile files
            }
        }

        return profiles;
    }

    public bool DeleteProfile(string profileName)
    {
        string filePath = GetProfileFilePath(profileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    public bool ProfileExists(string profileName)
    {
        return File.Exists(GetProfileFilePath(profileName));
    }

    public void ExportProfile(string profileName, string destinationFilePath)
    {
        string sourcePath = GetProfileFilePath(profileName);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Profile '{profileName}' does not exist.", sourcePath);

        string? dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.Copy(sourcePath, destinationFilePath, overwrite: true);
    }

    public WorkspaceProfile ImportProfile(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found.", sourceFilePath);

        string json = File.ReadAllText(sourceFilePath);
        var profile = JsonSerializer.Deserialize<WorkspaceProfile>(json, _jsonOptions)
            ?? throw new InvalidDataException("Invalid workspace profile format.");

        SaveProfile(profile);
        return profile;
    }

    private string GetProfileFilePath(string profileName)
    {
        var sanitized = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_profilesDirectory, $"{sanitized}.json");
    }
}
