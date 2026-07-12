using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

#nullable enable
namespace SimpleModManager
{
    public class PakMods : INotifyPropertyChanged
    {
        private string _pakName = "";
        private string _locationPath = "";
        private bool _isActive;
        private bool _hasConflict;

        public string PakName
        {
            get => _pakName;
            set { _pakName = value; OnPropertyChanged(); }
        }

        public string LocationPath
        {
            get => _locationPath;
            set { _locationPath = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActivationText));
                    OnPropertyChanged(nameof(StatusIndicatorBrush));
                    OnPropertyChanged(nameof(ActiveVisibility));
                    OnPropertyChanged(nameof(InactiveVisibility));
                }
            }
        }

        public bool HasConflict
        {
            get => _hasConflict;
            set
            {
                if (_hasConflict != value)
                {
                    _hasConflict = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActivationText));
                    OnPropertyChanged(nameof(StatusIndicatorBrush));
                }
            }
        }

        public string ActivationText => HasConflict && IsActive ? "CONFLICT" : IsActive ? "ENABLED" : "INACTIVE";

        public System.Windows.Media.Brush StatusIndicatorBrush => HasConflict && IsActive
            ? new SolidColorBrush(Color.FromRgb(255, 180, 0))
            : IsActive
                ? new SolidColorBrush(Color.FromRgb(199, 36, 93))
                : new SolidColorBrush(Color.FromRgb(76, 95, 95));

        public System.Windows.Visibility ActiveVisibility => IsActive
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility InactiveVisibility => IsActive
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

        private bool _isConfirmingDelete;

        public bool IsConfirmingDelete
        {
            get => _isConfirmingDelete;
            set
            {
                if (_isConfirmingDelete != value)
                {
                    _isConfirmingDelete = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DeleteOverlayVisibility));
                }
            }
        }

        public System.Windows.Visibility DeleteOverlayVisibility => IsConfirmingDelete
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
