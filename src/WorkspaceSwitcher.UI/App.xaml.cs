using System;
using System.Windows;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Services;
using WorkspaceSwitcher.UI.Services;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI;

public partial class App : Application
{
    private WindowManager? _windowManager;
    private ProfileService? _profileService;
    private SettingsService? _settingsService;
    private HotkeyManager? _hotkeyManager;
    private TrayIconService? _trayIconService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _windowManager = new WindowManager();
        _profileService = new ProfileService();
        _settingsService = new SettingsService();
        _hotkeyManager = new HotkeyManager();

        _mainViewModel = new MainViewModel(_windowManager, _profileService, _settingsService, _hotkeyManager);

        _mainWindow = new MainWindow(_mainViewModel);

        _trayIconService = new TrayIconService(
            _windowManager,
            _profileService,
            ShowWindow,
            ExitApplication
        );

        ShowWindow();
    }

    public void ShowWindow()
    {
        if (_mainWindow == null)
        {
            if (_mainViewModel != null)
            {
                _mainWindow = new MainWindow(_mainViewModel);
            }
        }

        if (_mainWindow != null)
        {
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
    }

    public void ExitApplication()
    {
        _trayIconService?.Dispose();
        _hotkeyManager?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _hotkeyManager?.Dispose();
        base.OnExit(e);
    }
}
