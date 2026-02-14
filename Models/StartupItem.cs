#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FancyStart.Models;

public class StartupItem : INotifyPropertyChanged
{
    private bool _isEnabled;
    private ImageSource? _icon;

    public string Name { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public StartupSourceType Source { get; set; }

    public string SourceDetail { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    public string RegistryKeyPath { get; set; } = string.Empty;

    public string RegistryValueName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string TaskPath { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
