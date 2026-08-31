using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using WorkspaceSwitcher.Core.Models;
using WorkspaceSwitcher.Core.Services;
using WorkspaceSwitcher.UI.Services;

namespace WorkspaceSwitcher.UI.ViewModels;

public class WindowItemViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;
    private readonly Action<WindowItemViewModel>? _onRemove;
    private bool _isExpanded;

    public WindowInfo Model { get; }
    public string ProcessName => Model.ProcessName;
    public string WindowTitle => string.IsNullOrWhiteSpace(Model.WindowTitle) ? Model.ProcessName : Model.WindowTitle;
    public string ExecutablePath => Model.ExecutablePath ?? "System / Background Process";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChevronIcon));
        }
    }

    public string ChevronIcon => IsExpanded ? "∧" : "⌵";

    public WindowState State
    {
        get => Model.Placement.State;
        set
        {
            if (Model.Placement.State != value)
            {
                Model.Placement.State = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StateText));
                _onChanged?.Invoke();
            }
        }
    }

    public string StateText => State.ToString();

    public IReadOnlyList<WindowState> AvailableStates { get; } = new[]
    {
        WindowState.Normal,
        WindowState.Maximized,
        WindowState.Minimized
    };

    public List<MonitorOption> AvailableMonitors { get; }

    public MonitorOption? SelectedMonitor
    {
        get
        {
            int left = Model.Placement.NormalPosition.Left;
            return AvailableMonitors.FirstOrDefault(m => left >= m.Left && left < m.Left + m.Width) 
                ?? AvailableMonitors.FirstOrDefault();
        }
        set
        {
            if (value != null)
            {
                var currentMon = SelectedMonitor;
                if (currentMon != null && currentMon.Index != value.Index)
                {
                    int offsetX = value.Left - currentMon.Left;
                    int offsetY = value.Top - currentMon.Top;

                    Model.Placement.NormalPosition.Left += offsetX;
                    Model.Placement.NormalPosition.Right += offsetX;
                    Model.Placement.NormalPosition.Top += offsetY;
                    Model.Placement.NormalPosition.Bottom += offsetY;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Left));
                    OnPropertyChanged(nameof(Top));
                    OnPropertyChanged(nameof(MonitorDisplay));
                    OnPropertyChanged(nameof(FormattedCoordinates));
                    _onChanged?.Invoke();
                }
            }
        }
    }

    public string MonitorDisplay
    {
        get
        {
            var mon = SelectedMonitor;
            return mon != null ? $"Monitor {mon.Index}" : "Monitor 1";
        }
    }

    public int Left
    {
        get => Model.Placement.NormalPosition.Left;
        set
        {
            int w = Width;
            Model.Placement.NormalPosition.Left = value;
            Model.Placement.NormalPosition.Right = value + w;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedCoordinates));
            OnPropertyChanged(nameof(MonitorDisplay));
            _onChanged?.Invoke();
        }
    }

    public int Top
    {
        get => Model.Placement.NormalPosition.Top;
        set
        {
            int h = Height;
            Model.Placement.NormalPosition.Top = value;
            Model.Placement.NormalPosition.Bottom = value + h;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedCoordinates));
            _onChanged?.Invoke();
        }
    }

    public int Width
    {
        get => Model.Placement.NormalPosition.Width;
        set
        {
            Model.Placement.NormalPosition.Right = Model.Placement.NormalPosition.Left + Math.Max(100, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedCoordinates));
            _onChanged?.Invoke();
        }
    }

    public int Height
    {
        get => Model.Placement.NormalPosition.Height;
        set
        {
            Model.Placement.NormalPosition.Bottom = Model.Placement.NormalPosition.Top + Math.Max(100, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedCoordinates));
            _onChanged?.Invoke();
        }
    }

    public string FormattedCoordinates
    {
        get
        {
            var p = Model.Placement.NormalPosition;
            return $"{p.Left}, {p.Top} • {p.Width} × {p.Height}";
        }
    }

    public ImageSource? AppIcon { get; }

    public ICommand ToggleExpandCommand { get; }
    public ICommand RemoveWindowCommand { get; }

    public WindowItemViewModel(
        WindowInfo windowInfo, 
        Action? onChanged = null, 
        Action<WindowItemViewModel>? onRemove = null)
    {
        Model = windowInfo;
        _onChanged = onChanged;
        _onRemove = onRemove;

        AppIcon = IconHelper.GetIconForExecutable(windowInfo.ExecutablePath, windowInfo.ProcessName);
        AvailableMonitors = MonitorService.GetConnectedMonitors();

        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        RemoveWindowCommand = new RelayCommand(() => _onRemove?.Invoke(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
