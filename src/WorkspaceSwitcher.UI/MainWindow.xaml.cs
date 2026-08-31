using System;
using System.ComponentModel;
using System.Windows;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Services;
using WorkspaceSwitcher.UI.Services;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI;

public partial class MainWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly ProfileService _profileService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;
    private readonly TrayIconService _trayIconService;
    private readonly MainViewModel _viewModel;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();

        _windowManager = new WindowManager();
        _profileService = new ProfileService();
        _settingsService = new SettingsService();
        _hotkeyManager = new HotkeyManager();

        _viewModel = new MainViewModel(_windowManager, _profileService, _settingsService, _hotkeyManager);
        DataContext = _viewModel;

        _trayIconService = new TrayIconService(
            _windowManager,
            _profileService,
            ShowWindow,
            ExplicitExit
        );
    }

    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }

    public void ExplicitExit()
    {
        _isExplicitExit = true;
        _trayIconService.Dispose();
        _hotkeyManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            _trayIconService.ShowNotification(
                "Workspace Switcher Active",
                "App is running in the background. Use global hotkeys (Ctrl+Alt+1..5) or the tray icon.",
                System.Windows.Forms.ToolTipIcon.Info
            );
        }
        else
        {
            _trayIconService.Dispose();
            _hotkeyManager.Dispose();
            base.OnClosing(e);
        }
    }
}
