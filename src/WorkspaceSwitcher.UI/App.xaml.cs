using System;
using System.Windows;
using System.Windows.Threading;

namespace WorkspaceSwitcher.UI;

using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

public partial class App : WpfApp
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WpfMessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Workspace Switcher - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WpfMessageBox.Show($"Fatal Error: {ex.Message}\n\n{ex.StackTrace}", "Workspace Switcher - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
