using System;
using System.Windows;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.UI.ViewModels;

namespace WorkspaceSwitcher.UI.Views;

public partial class WorkspaceDialog : Window
{
    public string WorkspaceName { get; private set; } = string.Empty;
    public string WorkspaceDescription { get; private set; } = string.Empty;
    public string WorkspaceIcon { get; private set; } = "💻";
    public string HotkeyModifier { get; private set; } = "Ctrl + Alt";
    public string HotkeyKey { get; private set; } = "Auto (1-5)";
    public bool CaptureTaskbar { get; private set; } = true;

    public WorkspaceDialog(
        string? initialName = null, 
        string? initialDescription = null, 
        string? initialIcon = null, 
        string? initialModifier = null, 
        string? initialKey = null, 
        bool isEditMode = false)
    {
        InitializeComponent();

        IconListBox.ItemsSource = ProfileItemViewModel.AvailableIcons;
        ModifierComboBox.ItemsSource = HotkeyHelper.AvailableModifiers;
        KeyComboBox.ItemsSource = HotkeyHelper.AvailableKeys;

        if (isEditMode)
        {
            Title = "Edit Workspace";
            HeaderIconText.Text = "✏️";
            HeaderTitleText.Text = "Edit Workspace";
            HeaderSubtitleText.Text = "Modify the name, description, icon or hotkey for this workspace.";
            PrimaryActionButton.Content = "💾 Save Changes";
            TaskbarOptionBorder.Visibility = Visibility.Collapsed;
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

        ModifierComboBox.SelectedItem = string.IsNullOrWhiteSpace(initialModifier) ? "Ctrl + Alt" : initialModifier;
        if (ModifierComboBox.SelectedItem == null) ModifierComboBox.SelectedIndex = 0;

        KeyComboBox.SelectedItem = string.IsNullOrWhiteSpace(initialKey) ? "Auto (1-5)" : initialKey;
        if (KeyComboBox.SelectedItem == null) KeyComboBox.SelectedIndex = 0;

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
        HotkeyModifier = ModifierComboBox.SelectedItem?.ToString() ?? "Ctrl + Alt";
        HotkeyKey = KeyComboBox.SelectedItem?.ToString() ?? "Auto (1-5)";
        CaptureTaskbar = CaptureTaskbarCheckBox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
