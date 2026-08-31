using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorkspaceSwitcher.Core.Hotkeys;
using WorkspaceSwitcher.Core.Models;

namespace WorkspaceSwitcher.UI.ViewModels;

public class ProfileItemViewModel : INotifyPropertyChanged
{
    private WorkspaceProfile _profile;
    private readonly int _colorIndex;
    private readonly Action<ProfileItemViewModel>? _onProfileUpdated;

    public ObservableCollection<WindowItemViewModel> WindowItems { get; } = new();

    public WorkspaceProfile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            ReloadWindowItems();
            NotifyAll();
        }
    }

    public string Name => _profile.Name;
    public string Description => string.IsNullOrWhiteSpace(_profile.Description) ? "workspace" : _profile.Description;
    
    public string IconGlyph
    {
        get => string.IsNullOrWhiteSpace(_profile.IconGlyph) ? "💻" : _profile.IconGlyph;
        set
        {
            if (_profile.IconGlyph != value)
            {
                _profile.IconGlyph = value;
                OnPropertyChanged();
                _onProfileUpdated?.Invoke(this);
            }
        }
    }

    public static IReadOnlyList<string> AvailableIcons { get; } = new[]
    {
        "💻", "🎮", "📚", "💼", "🎨", "🚀", "🌐", "⚙️", "🎬", "🎧", "⚡", "🔥", "🏆", "📱", "💡", "☕"
    };

    public string HotkeyModifier
    {
        get => string.IsNullOrWhiteSpace(_profile.HotkeyModifier) ? "Ctrl + Alt" : _profile.HotkeyModifier;
        set
        {
            if (_profile.HotkeyModifier != value)
            {
                _profile.HotkeyModifier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayHotkey));
                _onProfileUpdated?.Invoke(this);
            }
        }
    }

    public string HotkeyKey
    {
        get => _profile.HotkeyKey ?? "Auto (1-5)";
        set
        {
            if (_profile.HotkeyKey != value)
            {
                _profile.HotkeyKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayHotkey));
                _onProfileUpdated?.Invoke(this);
            }
        }
    }

    public string DisplayHotkey => HotkeyHelper.FormatDisplayHotkey(_profile.HotkeyModifier, _profile.HotkeyKey, _colorIndex);

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

    public ProfileItemViewModel(WorkspaceProfile profile, int index = 0, Action<ProfileItemViewModel>? onProfileUpdated = null)
    {
        _profile = profile;
        _colorIndex = index;
        _onProfileUpdated = onProfileUpdated;

        ReloadWindowItems();
    }

    private void ReloadWindowItems()
    {
        WindowItems.Clear();
        if (_profile.Windows != null)
        {
            foreach (var w in _profile.Windows)
            {
                WindowItems.Add(new WindowItemViewModel(
                    w,
                    onChanged: OnWindowChanged,
                    onRemove: OnWindowRemoved
                ));
            }
        }
    }

    private void OnWindowChanged()
    {
        OnPropertyChanged(nameof(WindowCount));
        OnPropertyChanged(nameof(MonitorCount));
        _onProfileUpdated?.Invoke(this);
    }

    private void OnWindowRemoved(WindowItemViewModel item)
    {
        WindowItems.Remove(item);
        _profile.Windows?.Remove(item.Model);
        OnPropertyChanged(nameof(WindowCount));
        OnPropertyChanged(nameof(MonitorCount));
        _onProfileUpdated?.Invoke(this);
    }

    public void NotifyAll()
    {
        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(WindowCount));
        OnPropertyChanged(nameof(MonitorCount));
        OnPropertyChanged(nameof(RelativeTime));
        OnPropertyChanged(nameof(WindowItems));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
