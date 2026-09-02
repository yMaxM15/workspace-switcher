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
    private readonly TaskbarService _taskbarService;

    private ProfileItemViewModel? _selectedProfile;
    private WorkspaceProfile? _activeProfile;
    private string _statusMessage = "Workspace Switcher is running in the background";
    private bool _autoLaunchMissingApps;
    private bool _minimizeToTrayOnClose;
    private bool _closeAppsOnSwitch;
    private bool _switchTaskbarPins;

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
            OnPropertyChanged(nameof(SelectedProfileTaskbarItems));
        }
    }

    public ObservableCollection<TaskbarItemViewModel>? SelectedProfileTaskbarItems => SelectedProfile?.TaskbarItems;

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

    private int _selectedInspectorTab = 0; // 0 = Windows, 1 = Taskbar

    public int SelectedInspectorTab
    {
        get => _selectedInspectorTab;
        set
        {
            if (_selectedInspectorTab != value)
            {
                _selectedInspectorTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWindowsTabSelected));
                OnPropertyChanged(nameof(IsTaskbarTabSelected));
            }
        }
    }

    public bool IsWindowsTabSelected => _selectedInspectorTab == 0;
    public bool IsTaskbarTabSelected => _selectedInspectorTab == 1;

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

    public bool SwitchTaskbarPins
    {
        get => _switchTaskbarPins;
        set
        {
            _switchTaskbarPins = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public int TotalProfilesCount => Profiles.Count;

    public IProfileService ProfileService => _profileService;
    public WindowManager WindowManager => _windowManager;
    public TaskbarService TaskbarService => _taskbarService;

    // Commands
    public ICommand OpenCreateWorkspaceDialogCommand { get; }
    public ICommand OpenEditWorkspaceDialogCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand OverwriteProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand RefreshProfilesCommand { get; }
    public ICommand CaptureTaskbarCommand { get; }
    public ICommand ApplyTaskbarCommand { get; }
    public ICommand SyncStaticPinsCommand { get; }
    public ICommand SelectWindowsTabCommand { get; }
    public ICommand SelectTaskbarTabCommand { get; }

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
        _taskbarService = new TaskbarService();

        var settings = _settingsService.Load();
        _autoLaunchMissingApps = settings.AutoLaunchMissingApps;
        _minimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        _closeAppsOnSwitch = settings.CloseAppsOnSwitch;
        _switchTaskbarPins = settings.SwitchTaskbarPins;

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
        CaptureTaskbarCommand = new RelayCommand<ProfileItemViewModel>(CaptureTaskbar);
        ApplyTaskbarCommand = new RelayCommand<ProfileItemViewModel>(ApplyTaskbar);
        SyncStaticPinsCommand = new RelayCommand(SyncStaticPins);
        SelectWindowsTabCommand = new RelayCommand(() => SelectedInspectorTab = 0);
        SelectTaskbarTabCommand = new RelayCommand(() => SelectedInspectorTab = 1);

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
            Profiles.Add(new ProfileItemViewModel(p, idx++, pvm => _profileService.SaveProfile(pvm.Profile), OnTaskbarStaticToggled));
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
                var staticPins = _settingsService.Load().StaticPinnedApps;
                var profile = _windowManager.CaptureWorkspace(
                    dlg.WorkspaceName, 
                    dlg.WorkspaceDescription, 
                    dlg.WorkspaceIcon, 
                    dlg.HotkeyModifier, 
                    dlg.HotkeyKey,
                    captureTaskbar: dlg.CaptureTaskbar,
                    staticAppIdentifiers: staticPins);
                _profileService.SaveProfile(profile);
                ActiveProfile = profile;
                SaveCurrentSettings();

                LoadProfiles();
                SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
                RegisterDefaultHotkeys();

                int taskbarCount = profile.Taskbar?.PinnedItems.Count ?? 0;
                string taskbarInfo = taskbarCount > 0 ? $", {taskbarCount} taskbar pin(s)" : "";
                StatusMessage = $"Workspace '{profile.Name}' captured with {profile.Windows.Count} window(s){taskbarInfo}!";
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
            var staticPins = _settingsService.Load().StaticPinnedApps;

            var (restoredCount, closedCount) = _windowManager.RestoreWorkspace(
                profile, 
                _autoLaunchMissingApps, 
                previousProfile: oldProfile, 
                closeAppsOnSwitch: _closeAppsOnSwitch,
                switchTaskbarPins: _switchTaskbarPins,
                staticAppIdentifiers: staticPins);

            ActiveProfile = profile;
            SaveCurrentSettings();

            string prefix = string.IsNullOrEmpty(source) ? "" : $"[{source}] ";
            string taskbarNote = (_switchTaskbarPins && profile.Taskbar != null && profile.Taskbar.Enabled)
                ? $", taskbar switched to {profile.Taskbar.PinnedItems.Count} pins"
                : "";

            if (closedCount > 0)
            {
                StatusMessage = $"{prefix}Switched to '{profile.Name}' ({restoredCount} repositioned, {closedCount} closed from '{oldProfile?.Name}'{taskbarNote}).";
            }
            else
            {
                StatusMessage = $"{prefix}Restored '{profile.Name}' ({restoredCount} windows repositioned{taskbarNote}).";
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
            var staticPins = _settingsService.Load().StaticPinnedApps;
            bool captureTaskbar = target.Profile.Taskbar?.Enabled ?? true;
            var updated = _windowManager.CaptureWorkspace(
                target.Name, 
                target.Profile.Description, 
                target.IconGlyph, 
                target.HotkeyModifier, 
                target.HotkeyKey,
                captureTaskbar: captureTaskbar,
                staticAppIdentifiers: staticPins);

            _profileService.SaveProfile(updated);
            target.Profile = updated;
            ActiveProfile = updated;
            SaveCurrentSettings();

            OnPropertyChanged(nameof(SelectedProfileWindowItems));
            OnPropertyChanged(nameof(SelectedProfileTaskbarItems));

            int taskbarCount = updated.Taskbar?.PinnedItems.Count ?? 0;
            string taskbarInfo = taskbarCount > 0 ? $", {taskbarCount} taskbar pin(s)" : "";
            StatusMessage = $"Updated '{target.Name}' with current layout ({updated.Windows.Count} windows{taskbarInfo}).";
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

    public void CaptureTaskbar(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null) return;

        try
        {
            var staticPins = _settingsService.Load().StaticPinnedApps;
            var config = _taskbarService.CaptureCurrentTaskbar(staticPins);
            target.Profile.Taskbar = config;
            _profileService.SaveProfile(target.Profile);
            target.ReloadTaskbarItems();
            target.NotifyAll();

            StatusMessage = $"Captured {config.PinnedItems.Count} taskbar pin(s) for '{target.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error capturing taskbar pins: {ex.Message}";
        }
    }

    public void ApplyTaskbar(ProfileItemViewModel? item)
    {
        var target = item ?? SelectedProfile;
        if (target == null || target.Profile.Taskbar == null)
        {
            StatusMessage = "No taskbar configuration captured for this workspace.";
            return;
        }

        try
        {
            var staticPins = _settingsService.Load().StaticPinnedApps;
            bool ok = _taskbarService.ApplyTaskbar(target.Profile.Taskbar, staticPins);
            StatusMessage = ok
                ? $"Applied taskbar layout for '{target.Name}' ({target.TaskbarItemCount} pinned apps)."
                : $"Failed to apply taskbar layout for '{target.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying taskbar: {ex.Message}";
        }
    }

    public void SyncStaticPins()
    {
        try
        {
            var settings = _settingsService.Load();
            var allProfiles = _profileService.GetAllProfiles();

            foreach (var p in allProfiles)
            {
                if (p.Taskbar?.PinnedItems == null) continue;
                foreach (var item in p.Taskbar.PinnedItems.Where(i => i.IsStatic).ToList())
                {
                    _taskbarService.SyncStaticItemAcrossProfiles(item, true, allProfiles, prof => _profileService.SaveProfile(prof));
                }
            }

            LoadProfiles();
            StatusMessage = $"Synchronized static taskbar pins across all workspaces.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error syncing static pins: {ex.Message}";
        }
    }

    private void OnTaskbarStaticToggled(TaskbarItemViewModel item, ProfileItemViewModel profile)
    {
        try
        {
            var settings = _settingsService.Load();
            if (item.IsStatic)
            {
                if (!settings.StaticPinnedApps.Contains(item.ShortcutFileName, StringComparer.OrdinalIgnoreCase))
                {
                    settings.StaticPinnedApps.Add(item.ShortcutFileName);
                }
            }
            else
            {
                settings.StaticPinnedApps.RemoveAll(x =>
                    string.Equals(x, item.ShortcutFileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x, item.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
            _settingsService.Save(settings);

            // Synchronize static status across other profiles
            var allProfiles = _profileService.GetAllProfiles();
            _taskbarService.SyncStaticItemAcrossProfiles(item.Model, item.IsStatic, allProfiles, p => _profileService.SaveProfile(p));

            // Reload other profiles in memory
            foreach (var pvm in Profiles)
            {
                if (pvm != profile)
                {
                    var updated = _profileService.LoadProfile(pvm.Name);
                    if (updated != null)
                    {
                        pvm.Profile = updated;
                    }
                }
            }

            StatusMessage = item.IsStatic
                ? $"'{item.DisplayName}' is now marked Static (preserved across all workspaces)."
                : $"'{item.DisplayName}' is now workspace-only for '{profile.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating static pin: {ex.Message}";
        }
    }

    private void SaveCurrentSettings()
    {
        var settings = _settingsService.Load();
        settings.AutoLaunchMissingApps = _autoLaunchMissingApps;
        settings.MinimizeToTrayOnClose = _minimizeToTrayOnClose;
        settings.CloseAppsOnSwitch = _closeAppsOnSwitch;
        settings.SwitchTaskbarPins = _switchTaskbarPins;
        settings.LastActiveProfileName = _activeProfile?.Name;
        _settingsService.Save(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
