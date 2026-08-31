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
    private string _statusMessage = "Workspace Switcher is running in the background";
    private bool _autoLaunchMissingApps;
    private bool _minimizeToTrayOnClose;

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

        if (SelectedProfile == null && Profiles.Count > 0)
        {
            SelectedProfile = Profiles[0];
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
                var profile = _windowManager.CaptureWorkspace(dlg.WorkspaceName, dlg.WorkspaceDescription, dlg.WorkspaceIcon);
                _profileService.SaveProfile(profile);

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
        var dlg = new WorkspaceDialog(target.Name, target.Description, target.IconGlyph, isEditMode: true)
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

                if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    _profileService.DeleteProfile(oldName);
                    target.Profile.Name = newName;
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
