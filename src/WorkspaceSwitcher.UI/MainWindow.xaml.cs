using System.ComponentModel;
using System.Windows;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }
}
