using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorkspaceSwitcher.Core.Models;

namespace WorkspaceSwitcher.UI.ViewModels;

public class ProfileItemViewModel : INotifyPropertyChanged
{
    private WorkspaceProfile _profile;

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
            OnPropertyChanged(nameof(LastModifiedDisplay));
        }
    }

    public string Name => _profile.Name;
    public string Description => string.IsNullOrWhiteSpace(_profile.Description) ? "No description provided" : _profile.Description;
    public int WindowCount => _profile.Windows?.Count ?? 0;
    public string LastModifiedDisplay => _profile.LastModifiedAt.ToLocalTime().ToString("g");

    public ProfileItemViewModel(WorkspaceProfile profile)
    {
        _profile = profile;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
