using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using PanomersiveViewerNET.Models;
using PanomersiveViewerNET.Properties;

namespace PanomersiveViewerNET
{
    /// <summary>
    /// Interaction logic for SelectCameraDialog.xaml
    /// </summary>
    public partial class SelectCameraDialog : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private List<CameraModel> _cameras = new List<CameraModel>();
        private string _ipAddress;
        private string _friendlyName;
        private string _username;
        private string _password;
        private CameraModel _selectedCamera;
        private bool _useHighResStream;

        /// <summary>
        /// Gets or sets the currently selected camera.
        /// </summary>
        public CameraModel SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                _selectedCamera = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the list of cameras added by the user.
        /// </summary>
        public List<CameraModel> Cameras
        {
            get => _cameras;
            set
            {
                _cameras = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the username to use for the camera connection.
        /// </summary>
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the password to use for the camera connection.
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the IP address of the camera.
        /// </summary>
        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the friendly name of the camera.
        /// </summary>
        public string FriendlyName
        {
            get => _friendlyName;
            set
            {
                _friendlyName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether the high resolution stream should be used.
        /// </summary>
        public bool UseHighResStream
        {
            get => _useHighResStream;
            set
            {
                _useHighResStream = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectCameraDialog"/> class.
        /// </summary>
        public SelectCameraDialog()
        {
            DataContext = this;
            InitializeComponent();

            if (Settings.Default.CameraCollection == null)
                Settings.Default.CameraCollection = new CameraCollectionModel();

            Cameras = Settings.Default.CameraCollection.Cameras;
            var selectedCamera = Settings.Default.SelectedCamera;
            SelectedCamera = Cameras.FirstOrDefault(info => info.IpAddress == selectedCamera);

            if (SelectedCamera != null)
                CameraComboBox.SelectedItem = SelectedCamera;
            else if (Cameras.Count > 0)
                CameraComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// The OnButtonClick event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnButtonClick(object sender, RoutedEventArgs args)
        {
            DialogResult = true;

            var item = Settings.Default.CameraCollection.Cameras.FirstOrDefault(info => info.IpAddress == IpAddress);
            if (item != null)
            {
                item.FriendlyName = FriendlyName;
                item.Username = Username;
                item.Password = Password;
                item.UseHighResStream = UseHighResStream;
                SelectedCamera = item;
            }
            else
            {
                SelectedCamera = new CameraModel(IpAddress, FriendlyName, Username, Password, UseHighResStream);
                Settings.Default.CameraCollection.Cameras.Add(SelectedCamera);
            }

            if (!string.IsNullOrEmpty(IpAddress))
                Settings.Default.SelectedCamera = IpAddress;

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

        /// <summary>
        /// The OnSelectionChanged event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The SelectionChangedEventArgs.</param>
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (args.AddedItems.Count == 0)
                return;

            SelectedCamera = args.AddedItems[0] as CameraModel;
            if (SelectedCamera == null)
                return;

            IpAddress = SelectedCamera.IpAddress;
            FriendlyName = SelectedCamera.FriendlyName;
            Username = SelectedCamera.Username;
            Password = SelectedCamera.Password;
            UseHighResStream = SelectedCamera.UseHighResStream;
        }
    }
}
