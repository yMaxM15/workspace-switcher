using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.Core.Services;

namespace WorkspaceSwitcher.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly WindowManager _windowManager;
    private readonly IProfileService _profileService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;

    private ProfileItemViewModel? _selectedProfile;
    private string _newProfileName = string.Empty;
    private string _newProfileDescription = string.Empty;
    private string _selectedNewIcon = "💻";
    private string _statusMessage = "Workspace Switcher is running in the background";
    private bool _autoLaunchMissingApps;
    private bool _minimizeToTrayOnClose;

    // Workspace Editing State
    private bool _isEditingWorkspace;
    private ProfileItemViewModel? _editingProfile;
    private string _editWorkspaceName = string.Empty;
    private string _editWorkspaceDescription = string.Empty;
    private string _editWorkspaceIcon = "💻";

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = new();

    public ProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedProfile));
            OnPropertyChanged(nameof(SelectedProfileWindowItems));
        }
    }

    public bool HasSelectedProfile => SelectedProfile != null;

    public ObservableCollection<WindowItemViewModel>? SelectedProfileWindowItems => SelectedProfile?.WindowItems;

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            _newProfileName = value;
            OnPropertyChanged();
        }
    }

    public string NewProfileDescription
    {
        get => _newProfileDescription;
        set
        {
            _newProfileDescription = value;
            OnPropertyChanged();
        }
    }

    public string SelectedNewIcon
    {
        get => _selectedNewIcon;
        set
        {
            _selectedNewIcon = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> AvailableIcons => ProfileItemViewModel.AvailableIcons;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool AutoLaunchMissingApps
    {
        get => _autoLaunchMissingApps;
        set
        {
            _autoLaunchMissingApps = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set
        {
            _minimizeToTrayOnClose = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public int TotalProfilesCount => Profiles.Count;

    // Editing Properties
    public bool IsEditingWorkspace
    {
        get => _isEditingWorkspace;
        set
        {
            _isEditingWorkspace = value;
            OnPropertyChanged();
        }
    }

    public string EditWorkspaceName
    {
        get => _editWorkspaceName;
        set
        {
            _editWorkspaceName = value;
            OnPropertyChanged();
        }
    }

    public string EditWorkspaceDescription
    {
        get => _editWorkspaceDescription;
        set
        {
            _editWorkspaceDescription = value;
            OnPropertyChanged();
        }
    }

    public string EditWorkspaceIcon
    {
        get => _editWorkspaceIcon;
        set
        {
            _editWorkspaceIcon = value;
            OnPropertyChanged();
        }
    }

    public IProfileService ProfileService => _profileService;
    public WindowManager WindowManager => _windowManager;

    // Commands
    public ICommand CaptureNewProfileCommand { get; }
    public ICommand PrepareNewWorkspaceCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand OverwriteProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand RefreshProfilesCommand { get; }
    public ICommand StartEditProfileCommand { get; }
    public ICommand SaveEditProfileCommand { get; }
    public ICommand CancelEditProfileCommand { get; }

    public MainViewModel(
        WindowManager windowManager,
        IProfileService profileService,
        SettingsService settingsService,
        HotkeyManager hotkeyManager)
    {
        _windowManager = windowManager;
        _profileService = profileService;
        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;

        var settings = _settingsService.Load();
        _autoLaunchMissingApps = settings.AutoLaunchMissingApps;
        _minimizeToTrayOnClose = settings.MinimizeToTrayOnClose;

        CaptureNewProfileCommand = new RelayCommand(CaptureNewProfile, () => !string.IsNullOrWhiteSpace(NewProfileName));
        PrepareNewWorkspaceCommand = new RelayCommand(PrepareNewWorkspace);
        ApplyProfileCommand = new RelayCommand<ProfileItemViewModel>(ApplyProfile);
        OverwriteProfileCommand = new RelayCommand<ProfileItemViewModel>(OverwriteProfile);
        DeleteProfileCommand = new RelayCommand<ProfileItemViewModel>(DeleteProfile);
        RefreshProfilesCommand = new RelayCommand(LoadProfiles);

        StartEditProfileCommand = new RelayCommand<ProfileItemViewModel>(StartEditProfile);
        SaveEditProfileCommand = new RelayCommand(SaveEditProfile, () => !string.IsNullOrWhiteSpace(EditWorkspaceName));
        CancelEditProfileCommand = new RelayCommand(() => IsEditingWorkspace = false);

        _hotkeyManager.HotKeyPressed += OnHotKeyPressed;

        LoadProfiles();
        RegisterDefaultHotkeys();
    }

    public void LoadProfiles()
    {
        Profiles.Clear();
        var allProfiles = _profileService.GetAllProfiles();
        int idx = 0;
        foreach (var p in allProfiles.OrderByDescending(p => p.LastModifiedAt))
        {
            Profiles.Add(new ProfileItemViewModel(p, idx++, pvm => _profileService.SaveProfile(pvm.Profile)));
        }

        OnPropertyChanged(nameof(TotalProfilesCount));

        if (SelectedProfile == null && Profiles.Count > 0)
        {
            SelectedProfile = Profiles[0];
        }
    }

    public void PrepareNewWorkspace()
    {
        NewProfileName = string.Empty;
        NewProfileDescription = string.Empty;
        SelectedNewIcon = "💻";
        IsEditingWorkspace = false;
        StatusMessage = "Enter workspace details and click 'Capture Current Windows'.";
    }

    public void CaptureNewProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;

        try
        {
            var profile = _windowManager.CaptureWorkspace(NewProfileName.Trim(), NewProfileDescription.Trim(), SelectedNewIcon);
            _profileService.SaveProfile(profile);

            NewProfileName = string.Empty;
            NewProfileDescription = string.Empty;
            SelectedNewIcon = "💻";

            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
            RegisterDefaultHotkeys();

            StatusMessage = $"Workspace '{profile.Name}' created with {profile.Windows.Count} window(s)!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error capturing profile: {ex.Message}";
        }
    }

    public void StartEditProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        _editingProfile = target;
        EditWorkspaceName = target.Name;
        EditWorkspaceDescription = target.Description;
        EditWorkspaceIcon = target.IconGlyph;
        IsEditingWorkspace = true;
        StatusMessage = $"Editing workspace '{target.Name}'...";
    }

    public void SaveEditProfile()
    {
        if (_editingProfile == null || string.IsNullOrWhiteSpace(EditWorkspaceName)) return;

        try
        {
            string oldName = _editingProfile.Name;
            string newName = EditWorkspaceName.Trim();

            _editingProfile.Profile.Description = EditWorkspaceDescription.Trim();
            _editingProfile.Profile.IconGlyph = EditWorkspaceIcon;

            if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                _profileService.DeleteProfile(oldName);
                _editingProfile.Profile.Name = newName;
            }

            _profileService.SaveProfile(_editingProfile.Profile);
            _editingProfile.NotifyAll();

            IsEditingWorkspace = false;
            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
            RegisterDefaultHotkeys();

            StatusMessage = $"Workspace '{newName}' updated successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving workspace: {ex.Message}";
        }
    }

    public void ApplyProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        try
        {
            int count = _windowManager.RestoreWorkspace(target.Profile, _autoLaunchMissingApps);
            StatusMessage = $"Restored '{target.Name}' ({count} windows repositioned).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying profile: {ex.Message}";
        }
    }

    public void OverwriteProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        try
        {
            var updated = _windowManager.CaptureWorkspace(target.Name, target.Profile.Description, target.IconGlyph);
            _profileService.SaveProfile(updated);
            target.Profile = updated;
            OnPropertyChanged(nameof(SelectedProfileWindowItems));
            StatusMessage = $"Updated '{target.Name}' with current layout ({updated.Windows.Count} windows).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating profile: {ex.Message}";
        }
    }

    public void DeleteProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        try
        {
            _profileService.DeleteProfile(target.Name);
            var match = Profiles.FirstOrDefault(p => p.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                Profiles.Remove(match);
            }
            SelectedProfile = Profiles.FirstOrDefault();
            OnPropertyChanged(nameof(TotalProfilesCount));
            RegisterDefaultHotkeys();
            StatusMessage = $"Workspace '{target.Name}' deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting profile: {ex.Message}";
        }
    }

    private void RegisterDefaultHotkeys()
    {
        try
        {
            _hotkeyManager.UnregisterAll();

            uint currentKey = 0x31; // '1'
            foreach (var p in Profiles.Take(5))
            {
                _hotkeyManager.Register(KeyModifiers.Control | KeyModifiers.Alt, currentKey, p.Name, HotKeyAction.RestoreProfile);
                currentKey++;
            }
        }
        catch
        {
            // Ignore if hotkeys conflict with another tool
        }
    }

    private void OnHotKeyPressed(object? sender, HotKeyEventArgs e)
    {
        if (e.Binding != null && e.Binding.Action == HotKeyAction.RestoreProfile)
        {
            var profile = _profileService.LoadProfile(e.Binding.TargetProfileName);
            if (profile != null)
            {
                int count = _windowManager.RestoreWorkspace(profile, _autoLaunchMissingApps);
                App.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"[Hotkey] Restored '{profile.Name}' ({count} windows repositioned).";
                });
            }
        }
    }

    private void SaveCurrentSettings()
    {
        var settings = new AppSettings
        {
            AutoLaunchMissingApps = _autoLaunchMissingApps,
            MinimizeToTrayOnClose = _minimizeToTrayOnClose
        };
        _settingsService.Save(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
