using System.Collections.Generic;
using System.Threading.Tasks;
using WorkspaceSwitcher.Core.Models;

namespace WorkspaceSwitcher.Core.Services;

public interface IProfileService
{
    string ProfilesDirectory { get; }

    void SaveProfile(WorkspaceProfile profile);
    Task SaveProfileAsync(WorkspaceProfile profile);

    WorkspaceProfile? LoadProfile(string profileName);
    Task<WorkspaceProfile?> LoadProfileAsync(string profileName);

    IReadOnlyList<string> GetProfileNames();
    IReadOnlyList<WorkspaceProfile> GetAllProfiles();
    Task<IReadOnlyList<WorkspaceProfile>> GetAllProfilesAsync();

    bool DeleteProfile(string profileName);
    bool ProfileExists(string profileName);

    void ExportProfile(string profileName, string destinationFilePath);
    WorkspaceProfile ImportProfile(string sourceFilePath);
}
