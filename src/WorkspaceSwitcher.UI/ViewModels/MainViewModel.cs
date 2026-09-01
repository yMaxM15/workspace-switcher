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
using WorkspaceSwitcher.UI.Views;

namespace WorkspaceSwitcher.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly WindowManager _windowManager;
    private readonly IProfileService _profileService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;

    private ProfileItemViewModel? _selectedProfile;
    private WorkspaceProfile? _activeProfile;
    private string _statusMessage = "Workspace Switcher is running in the background";
    private bool _autoLaunchMissingApps;
    private bool _minimizeToTrayOnClose;
    private bool _closeAppsOnSwitch;

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

    public WorkspaceProfile? ActiveProfile
    {
        get => _activeProfile;
        private set
        {
            _activeProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveProfileName));
            UpdateActiveProfilesInList();
        }
    }

    public string? ActiveProfileName => _activeProfile?.Name;

    public bool HasSelectedProfile => SelectedProfile != null;

    public ObservableCollection<WindowItemViewModel>? SelectedProfileWindowItems => SelectedProfile?.WindowItems;

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

    public bool CloseAppsOnSwitch
    {
        get => _closeAppsOnSwitch;
        set
        {
            _closeAppsOnSwitch = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public int TotalProfilesCount => Profiles.Count;

    public IProfileService ProfileService => _profileService;
    public WindowManager WindowManager => _windowManager;

    // Commands
    public ICommand OpenCreateWorkspaceDialogCommand { get; }
    public ICommand OpenEditWorkspaceDialogCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand OverwriteProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand RefreshProfilesCommand { get; }

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
        _closeAppsOnSwitch = settings.CloseAppsOnSwitch;

        if (!string.IsNullOrWhiteSpace(settings.LastActiveProfileName))
        {
            _activeProfile = _profileService.LoadProfile(settings.LastActiveProfileName);
        }

        OpenCreateWorkspaceDialogCommand = new RelayCommand(OpenCreateWorkspaceDialog);
        OpenEditWorkspaceDialogCommand = new RelayCommand<ProfileItemViewModel>(OpenEditWorkspaceDialog);
        ApplyProfileCommand = new RelayCommand<ProfileItemViewModel>(ApplyProfile);
        OverwriteProfileCommand = new RelayCommand<ProfileItemViewModel>(OverwriteProfile);
        DeleteProfileCommand = new RelayCommand<ProfileItemViewModel>(DeleteProfile);
        RefreshProfilesCommand = new RelayCommand(LoadProfiles);

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
        UpdateActiveProfilesInList();

        if (SelectedProfile == null && Profiles.Count > 0)
        {
            SelectedProfile = Profiles[0];
        }
    }

    private void UpdateActiveProfilesInList()
    {
        foreach (var p in Profiles)
        {
            p.IsActive = _activeProfile != null && string.Equals(p.Name, _activeProfile.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void OpenCreateWorkspaceDialog()
    {
        var owner = App.Current?.MainWindow;
        var dlg = new WorkspaceDialog(isEditMode: false)
        {
            Owner = owner
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var profile = _windowManager.CaptureWorkspace(
                    dlg.WorkspaceName, 
                    dlg.WorkspaceDescription, 
                    dlg.WorkspaceIcon, 
                    dlg.HotkeyModifier, 
                    dlg.HotkeyKey);
                _profileService.SaveProfile(profile);
                ActiveProfile = profile;
                SaveCurrentSettings();

                LoadProfiles();
                SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
                RegisterDefaultHotkeys();

                StatusMessage = $"Workspace '{profile.Name}' captured with {profile.Windows.Count} window(s)!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error capturing workspace: {ex.Message}";
            }
        }
    }

    public void OpenEditWorkspaceDialog(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        var owner = App.Current?.MainWindow;
        var dlg = new WorkspaceDialog(
            target.Name, 
            target.Description, 
            target.IconGlyph, 
            target.HotkeyModifier, 
            target.HotkeyKey, 
            isEditMode: true)
        {
            Owner = owner
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                string oldName = target.Name;
                string newName = dlg.WorkspaceName;

                target.Profile.Description = dlg.WorkspaceDescription;
                target.Profile.IconGlyph = dlg.WorkspaceIcon;
                target.Profile.HotkeyModifier = dlg.HotkeyModifier;
                target.Profile.HotkeyKey = dlg.HotkeyKey;

                if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    _profileService.DeleteProfile(oldName);
                    target.Profile.Name = newName;

                    if (_activeProfile != null && string.Equals(_activeProfile.Name, oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        ActiveProfile = target.Profile;
                        SaveCurrentSettings();
                    }
                }

                _profileService.SaveProfile(target.Profile);
                target.NotifyAll();

                LoadProfiles();
                SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                RegisterDefaultHotkeys();

                StatusMessage = $"Workspace '{newName}' updated successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating workspace: {ex.Message}";
            }
        }
    }

    public void SwitchToWorkspace(WorkspaceProfile profile, string source = "")
    {
        if (profile == null) return;

        try
        {
            var oldProfile = _activeProfile;
            var (restoredCount, closedCount) = _windowManager.RestoreWorkspace(
                profile, 
                _autoLaunchMissingApps, 
                previousProfile: oldProfile, 
                closeAppsOnSwitch: _closeAppsOnSwitch);

            ActiveProfile = profile;
            SaveCurrentSettings();

            string prefix = string.IsNullOrEmpty(source) ? "" : $"[{source}] ";
            if (closedCount > 0)
            {
                StatusMessage = $"{prefix}Switched to '{profile.Name}' ({restoredCount} repositioned, {closedCount} closed from '{oldProfile?.Name}').";
            }
            else
            {
                StatusMessage = $"{prefix}Restored '{profile.Name}' ({restoredCount} windows repositioned).";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error switching workspace: {ex.Message}";
        }
    }

    public void ApplyProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        SwitchToWorkspace(target.Profile);
    }

    public void OverwriteProfile(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        try
        {
            var updated = _windowManager.CaptureWorkspace(target.Name, target.Profile.Description, target.IconGlyph, target.HotkeyModifier, target.HotkeyKey);
            _profileService.SaveProfile(updated);
            target.Profile = updated;
            ActiveProfile = updated;
            SaveCurrentSettings();

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
            if (_activeProfile != null && string.Equals(_activeProfile.Name, target.Name, StringComparison.OrdinalIgnoreCase))
            {
                ActiveProfile = null;
                SaveCurrentSettings();
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

            int idx = 0;
            foreach (var p in Profiles)
            {
                var mods = HotkeyHelper.ParseModifiers(p.HotkeyModifier);
                uint vk = HotkeyHelper.ParseVirtualKey(p.HotkeyKey, idx);

                if (vk != 0)
                {
                    try
                    {
                        _hotkeyManager.Register(mods, vk, p.Name, HotKeyAction.RestoreProfile);
                    }
                    catch
                    {
                        // Ignore individual hotkey collision
                    }
                }
                idx++;
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
                App.Current?.Dispatcher.Invoke(() =>
                {
                    SwitchToWorkspace(profile, source: "Hotkey");
                });
            }
        }
    }

    private void SaveCurrentSettings()
    {
        var settings = _settingsService.Load();
        settings.AutoLaunchMissingApps = _autoLaunchMissingApps;
        settings.MinimizeToTrayOnClose = _minimizeToTrayOnClose;
        settings.CloseAppsOnSwitch = _closeAppsOnSwitch;
        settings.LastActiveProfileName = _activeProfile?.Name;
        _settingsService.Save(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
