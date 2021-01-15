using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PanomersiveViewerNET.Properties;

namespace PanomersiveViewerNET
{
    /// <summary>
    /// Interaction logic for CameraTiltDialog.xaml
    /// </summary>
    public partial class CameraTiltDialog : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _cameraTiltAngle;
        private bool _overrideCameraTilt;
        private readonly SessionManager _sessionManager;

        /// <summary>
        /// Gets or sets whether the camera tilt override is enabled.
        /// </summary>
        public bool OverrideCameraTilt
        {
            get => _overrideCameraTilt;
            set
            {
                _overrideCameraTilt = value;
                _sessionManager.AdjustCameraTilt(_cameraTiltAngle, _overrideCameraTilt);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the camera tilt override angle.
        /// </summary>
        public int CameraTiltAngle
        {
            get => _cameraTiltAngle;
            set
            {
                _cameraTiltAngle = value;
                _sessionManager.AdjustCameraTilt(_cameraTiltAngle, _overrideCameraTilt);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraTiltDialog"/> class.
        /// </summary>
        /// <param name="sessionManager">The SessionManager instance.</param>
        public CameraTiltDialog(SessionManager sessionManager)
        {
            DataContext = this;
            _cameraTiltAngle = Settings.Default.CameraTiltAngle;
            _overrideCameraTilt = Settings.Default.OverrideCameraTilt;
            _sessionManager = sessionManager;
            InitializeComponent();
        }

        /// <summary>
        /// The OnButtonClick event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnButtonClick(object sender, RoutedEventArgs args)
        {
            DialogResult = true;

            Settings.Default.OverrideCameraTilt = OverrideCameraTilt;
            Settings.Default.CameraTiltAngle = CameraTiltAngle;
            Settings.Default.Save();
        }

        /// <summary>
        /// The OnPropertyChanged event handler.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
