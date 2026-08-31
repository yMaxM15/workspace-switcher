using System;
using System.Windows;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI.Views;

public partial class WorkspaceDialog : Window
{
    public string WorkspaceName { get; private set; } = string.Empty;
    public string WorkspaceDescription { get; private set; } = string.Empty;
    public string WorkspaceIcon { get; private set; } = "💻";

    public WorkspaceDialog(string? initialName = null, string? initialDescription = null, string? initialIcon = null, bool isEditMode = false)
    {
        InitializeComponent();

        IconListBox.ItemsSource = ProfileItemViewModel.AvailableIcons;

        if (isEditMode)
        {
            Title = "Edit Workspace";
            HeaderIconText.Text = "✏️";
            HeaderTitleText.Text = "Edit Workspace";
            HeaderSubtitleText.Text = "Modify the name, description or icon for this workspace.";
            PrimaryActionButton.Content = "💾 Save Changes";
        }
        else
        {
            Title = "Create Workspace";
            HeaderIconText.Text = "📷";
            HeaderTitleText.Text = "Create Workspace";
            HeaderSubtitleText.Text = "Capture your current multi-monitor window layout into a saved profile.";
            PrimaryActionButton.Content = "📸 Capture & Save";
        }

        NameTextBox.Text = initialName ?? string.Empty;
        DescriptionTextBox.Text = initialDescription ?? string.Empty;
        
        string iconToSelect = string.IsNullOrWhiteSpace(initialIcon) ? "💻" : initialIcon;
        IconListBox.SelectedItem = iconToSelect;
        if (IconListBox.SelectedItem == null && ProfileItemViewModel.AvailableIcons.Count > 0)
        {
            IconListBox.SelectedItem = ProfileItemViewModel.AvailableIcons[0];
        }

        Loaded += (s, e) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            System.Windows.MessageBox.Show("Please enter a workspace name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        WorkspaceName = name;
        WorkspaceDescription = DescriptionTextBox.Text.Trim();
        WorkspaceIcon = IconListBox.SelectedItem?.ToString() ?? "💻";

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
