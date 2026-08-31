using System;
using System.Windows;
using System.Windows.Threading;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Services;
using WorkspaceSwitcher.UI.Services;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI;

using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

public partial class App : WpfApp
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

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            _windowManager = new WindowManager();
            _profileService = new ProfileService();
            _settingsService = new SettingsService();
            _hotkeyManager = new HotkeyManager();

            _mainViewModel = new MainViewModel(_windowManager, _profileService, _settingsService, _hotkeyManager);

            _mainWindow = new MainWindow(_mainViewModel);
            MainWindow = _mainWindow;

            _trayIconService = new TrayIconService(
                _windowManager,
                _profileService,
                ShowWindow,
                ExitApplication
            );

            ShowWindow();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Application Startup Error:\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                "Workspace Switcher - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown(1);
        }
    }

    public void ShowWindow()
    {
        if (_mainWindow == null)
        {
            if (_mainViewModel != null)
            {
                _mainWindow = new MainWindow(_mainViewModel);
                MainWindow = _mainWindow;
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WpfMessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WpfMessageBox.Show($"Fatal Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
