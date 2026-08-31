using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorkspaceSwitcher.Core.Models;

namespace WorkspaceSwitcher.UI.ViewModels;

public class ProfileItemViewModel : INotifyPropertyChanged
{
    private WorkspaceProfile _profile;
    private readonly int _colorIndex;

    public WorkspaceProfile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(WindowCount));
            OnPropertyChanged(nameof(MonitorCount));
            OnPropertyChanged(nameof(RelativeTime));
            OnPropertyChanged(nameof(WindowItems));
        }
    }

    public string Name => _profile.Name;
    public string Description => string.IsNullOrWhiteSpace(_profile.Description) ? "workspace" : _profile.Description;
    public int WindowCount => _profile.Windows?.Count ?? 0;

    public int MonitorCount
    {
        get
        {
            if (_profile.Windows == null || _profile.Windows.Count == 0) return 1;
            var monitors = new HashSet<int>();
            foreach (var w in _profile.Windows)
            {
                int left = w.Placement.NormalPosition.Left;
                if (left < 0) monitors.Add(2);
                else if (left >= 1920) monitors.Add(3);
                else monitors.Add(1);
            }
            return Math.Max(1, monitors.Count);
        }
    }

    public string RelativeTime
    {
        get
        {
            var local = _profile.LastModifiedAt.ToLocalTime();
            var diff = DateTime.Now - local;

            if (diff.TotalDays < 1 && local.Date == DateTime.Today)
            {
                return $"Today, {local:HH:mm}";
            }
            if (local.Date == DateTime.Today.AddDays(-1))
            {
                return $"Yesterday, {local:HH:mm}";
            }
            if (diff.TotalDays < 7)
            {
                return $"{(int)diff.TotalDays} days ago, {local:HH:mm}";
            }
            return local.ToString("dd. MMM, HH:mm");
        }
    }

    public System.Windows.Media.Brush IconBrush
    {
        get
        {
            return (_colorIndex % 4) switch
            {
                0 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241)), // Indigo
                1 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)), // Emerald Green
                2 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)), // Blue
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246))  // Purple
            };
        }
    }

    public IReadOnlyList<WindowItemViewModel> WindowItems
    {
        get
        {
            if (_profile.Windows == null) return Array.Empty<WindowItemViewModel>();
            return _profile.Windows.Select(w => new WindowItemViewModel(w)).ToList();
        }
    }

    public ProfileItemViewModel(WorkspaceProfile profile, int index = 0)
    {
        _profile = profile;
        _colorIndex = index;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
