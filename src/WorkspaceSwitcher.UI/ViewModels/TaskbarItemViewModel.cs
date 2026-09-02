using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.UI.Services;

namespace WorkspaceSwitcher.UI.ViewModels;

public class TaskbarItemViewModel : INotifyPropertyChanged
{
    private readonly TaskbarPinnedItem _model;
    private readonly Action<TaskbarItemViewModel>? _onStaticToggled;
    private readonly Action<TaskbarItemViewModel>? _onRemove;
    private ImageSource? _icon;
    private bool _iconLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskbarPinnedItem Model => _model;

    public string DisplayName => _model.DisplayName;
    public string ShortcutFileName => _model.ShortcutFileName;
    public string? TargetPath => _model.TargetPath;

    public bool IsStatic
    {
        get => _model.IsStatic;
        set
        {
            if (_model.IsStatic != value)
            {
                _model.IsStatic = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StaticStatusText));
                OnPropertyChanged(nameof(IsWorkspaceOnly));
                _onStaticToggled?.Invoke(this);
            }
        }
    }

    public bool IsWorkspaceOnly => !IsStatic;

    public string StaticStatusText => IsStatic ? "📌 Static (All Workspaces)" : "🗔 Workspace Only";

    public ImageSource? Icon
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _icon = IconHelper.GetIconForShortcut(_model.ShortcutFileName, _model.TargetPath);
            }
            return _icon;
        }
    }

    public ICommand ToggleStaticCommand { get; }
    public ICommand RemoveCommand { get; }

    public TaskbarItemViewModel(
        TaskbarPinnedItem model,
        Action<TaskbarItemViewModel>? onStaticToggled = null,
        Action<TaskbarItemViewModel>? onRemove = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _onStaticToggled = onStaticToggled;
        _onRemove = onRemove;

        ToggleStaticCommand = new RelayCommand(() => IsStatic = !IsStatic);
        RemoveCommand = new RelayCommand(() => _onRemove?.Invoke(this));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
