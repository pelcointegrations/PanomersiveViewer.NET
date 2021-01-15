using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using PanomersiveViewerNET.Properties;
using PanomersiveViewerNET.Utils;
using ImExTkNet;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace PanomersiveViewerNET
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : IDisposable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _hasPanned;
        private bool _isAutoPanTiltEnabled;
        private bool _isAutoZoomInEnabled;
        private bool _isAutoZoomOutEnabled;
        private bool _isPanTilting;
        private System.Drawing.Point _ptzAnchor;
        private LayoutOptions _selectedLayout;
        private StreamOptimizedTypes _selectedOptimizationType;
        private PtzLimitMode _selectedPtzMode;
        private SessionManager _sessionManager;
        private bool _showFps;
        private string _windowTitle = "Pelco Camera Viewer";

        /// <summary>
        /// Gets or sets the currently selected layout type.
        /// </summary>
        public LayoutOptions SelectedLayout
        {
            get => _selectedLayout;
            set
            {
                _selectedLayout = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected stream optimization type.
        /// </summary>
        public StreamOptimizedTypes SelectedOptimizationType
        {
            get => _selectedOptimizationType;
            set
            {
                _selectedOptimizationType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected PTZ limit mode.
        /// </summary>
        public PtzLimitMode SelectedPtzMode
        {
            get => _selectedPtzMode;
            set
            {
                _selectedPtzMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether PTZ controls will auto-zoom-in upon approaching data edge.
        /// </summary>
        public bool IsAutoZoomInEnabled
        {
            get => _isAutoZoomInEnabled;
            set
            {
                _isAutoZoomInEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether PTZ controls will auto-zoom-out upon leaving data edge.
        /// </summary>
        public bool IsAutoZoomOutEnabled
        {
            get => _isAutoZoomOutEnabled;
            set
            {
                _isAutoZoomOutEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether PTZ controls will auto-pan/tilt on zoom out from data edge.
        /// </summary>
        public bool IsAutoPanTiltEnabled
        {
            get => _isAutoPanTiltEnabled;
            set
            {
                _isAutoPanTiltEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether the current frame rate will be displayed in an overlay.
        /// </summary>
        public bool ShowFps
        {
            get => _showFps;
            set
            {
                _showFps = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the window title text.
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            // Load the settings from the last saved session.
            SelectedOptimizationType = Settings.Default.StreamOptimizedType;
            SelectedPtzMode = Settings.Default.PtzLimitMode;
            IsAutoZoomInEnabled = Settings.Default.AutoZoomIn;
            IsAutoZoomOutEnabled = Settings.Default.AutoZoomOut;
            IsAutoPanTiltEnabled = Settings.Default.AutoPanTilt;
            ShowFps = Settings.Default.ShowFps;
            InitializeComponent();
            DataContext = this;

            // Restore the layout from the last saved session.
            OnSelectViewLayout(null, null);

            // Share the OpenGL context between the Immersive and Panoramic windows.
            PanoramicWindow.SetGlContext(ImmersiveWindow.GetContext());
        }

        /// <summary>
        /// Disposes this instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public void Dispose()
        {
            _sessionManager?.Dispose();
            ImmersiveWindow?.Release();
        }

        /// <summary>
        /// The OnAdjustCameraTilt event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnAdjustCameraTilt(object sender, RoutedEventArgs args)
        {
            var dialog = new CameraTiltDialog(_sessionManager);
            if (dialog.ShowDialog() == true)
                return;

            _sessionManager.AdjustCameraTilt(Settings.Default.CameraTiltAngle, Settings.Default.OverrideCameraTilt);
        }

        /// <summary>
        /// The OnMouseClick event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        private void OnMouseClick(object sender, MouseEventArgs args)
        {
            if (_hasPanned)
            {
                _hasPanned = false;
                return;
            }

            var window = sender as OpenGlWindow;
            _sessionManager.MoveToPosition(args.Location, (string)window?.Tag == nameof(PanoramicWindow));
        }

        /// <summary>
        /// The OnMouseDoubleClick event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        private void OnMouseDoubleClick(object sender, MouseEventArgs args)
        {
            if (_hasPanned)
            {
                // Prevent calling MoveToPosition after a mouse drag pan/tilt.
                _hasPanned = false;
                return;
            }

            var window = sender as OpenGlWindow;
            _sessionManager.MoveToPosition(args.Location, (string)window?.Tag == nameof(PanoramicWindow), true);
        }

        /// <summary>
        /// The OnMouseDown event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        private void OnMouseDown(object sender, MouseEventArgs args)
        {
            _ptzAnchor = args.Location;
            _isPanTilting = true;
        }

        /// <summary>
        /// The OnMouseMove event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        private void OnMouseMove(object sender, MouseEventArgs args)
        {
            if (!(sender is OpenGlWindow window))
                return;

            var isPanoramic = (string)window.Tag == nameof(PanoramicWindow);
            if (isPanoramic && SelectedLayout != LayoutOptions.Panoramic)
                return;

            // If the mouse moves outside the window during panTilt, stop the operation.
            // This leaves the view where it was last dragged rather than having it snap back
            // to its original position if you drag too far. Much nicer for the user.
            if (_isPanTilting && _ptzAnchor != args.Location && window.DisplayRectangle.Contains(new System.Drawing.Point(args.Location.X, args.Location.Y)))
            {
                try
                {
                    // We are doing a pan/tilt action, so set _hasPanned to avoid sending a MoveToPosition call on MouseUp.
                    _hasPanned = true;
                    _sessionManager.PerformPanTilt(_ptzAnchor, args.Location, isPanoramic);
                }
                catch
                {
                    // Ignore errors here, likely caused by the mouse moving too far outside the view area.
                }
            }

            _ptzAnchor = args.Location;
        }

        /// <summary>
        /// The OnMouseUp event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        private void OnMouseUp(object sender, MouseEventArgs args)
        {
            if (_isPanTilting)
                _isPanTilting = false;
        }

        /// <summary>
        /// The OnMouseWheel event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The MouseEventArgs.</param>
        protected void OnMouseWheel(object sender, MouseEventArgs args)
        {
            var window = sender as OpenGlWindow;
            _sessionManager.PerformZoom(args.Location, args.Delta, (string)window?.Tag == nameof(PanoramicWindow));
        }

        /// <summary>
        /// The OnOpenCameraStream event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnOpenCameraStream(object sender, RoutedEventArgs args)
        {
            var inputDialog = new SelectCameraDialog();
            if (inputDialog.ShowDialog() != true)
                return;

            WindowTitle = $"Pelco Camera Viewer - {inputDialog.SelectedCamera.DisplayName}";
            try
            {
                _sessionManager?.Dispose();
                _sessionManager = null;
                _sessionManager = new SessionManager(ImmersiveWindow, PanoramicWindow);
                _sessionManager.LoadCameraStream(inputDialog.SelectedCamera);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// The OnOpenLocalFile event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnOpenLocalFile(object sender, RoutedEventArgs args)
        {
            var sourceType = ((MenuItem)sender).Tag;
            SelectFixedSource((CameraStreamType)sourceType);
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
        /// The OnSelectAutoPanTilt event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectAutoPanTilt(object sender, RoutedEventArgs args)
        {
            IsAutoPanTiltEnabled = !IsAutoPanTiltEnabled;
            Settings.Default.AutoPanTilt = IsAutoPanTiltEnabled;
            Settings.Default.Save();
            Helpers.SetPtzLimitOptions(_sessionManager.ImmersiveView, _sessionManager.PanoramicView);
        }

        /// <summary>
        /// The OnSelectAutoZoomIn event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectAutoZoomIn(object sender, RoutedEventArgs args)
        {
            IsAutoZoomInEnabled = !IsAutoZoomInEnabled;
            Settings.Default.AutoZoomIn = IsAutoZoomInEnabled;
            Settings.Default.Save();
            Helpers.SetPtzLimitOptions(_sessionManager.ImmersiveView, _sessionManager.PanoramicView);
        }

        /// <summary>
        /// The OnSelectAutoZoomOut event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectAutoZoomOut(object sender, RoutedEventArgs args)
        {
            IsAutoZoomOutEnabled = !IsAutoZoomOutEnabled;
            Settings.Default.AutoZoomOut = IsAutoZoomOutEnabled;
            Settings.Default.Save();
            Helpers.SetPtzLimitOptions(_sessionManager.ImmersiveView, _sessionManager.PanoramicView);
        }

        /// <summary>
        /// The OnSelectExit event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectExit(object sender, RoutedEventArgs args)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// The OnSelectOptimizationMode event handler.
        /// Sets the stream optimization type and re-initializes the OpenGL instances.
        /// </summary>
        /// <param name="sender">The stream optimization type to set.</param>
        /// <param name="args">The stream optimization type to set.</param>
        private void OnSelectOptimizationMode(object sender, RoutedEventArgs args)
        {
            var optimizationType = (StreamOptimizedTypes)((MenuItem)sender).Tag;
            SelectedOptimizationType = optimizationType;
            Settings.Default.StreamOptimizedType = SelectedOptimizationType;
            Settings.Default.Save();
            _sessionManager.CleanupVideo();
            _sessionManager.SetupVideo();
            _sessionManager.RefreshViews();
        }

        /// <summary>
        /// The OnSelectPtzLimitMode event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectPtzLimitMode(object sender, RoutedEventArgs args)
        {
            var type = ((MenuItem)sender).Tag;
            SelectedPtzMode = (PtzLimitMode)type;
            Settings.Default.PtzLimitMode = SelectedPtzMode;
            Settings.Default.Save();
            Helpers.SetPtzLimitOptions(_sessionManager.ImmersiveView, _sessionManager.PanoramicView);
        }

        /// <summary>
        /// The OnSelectShowFps event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnSelectShowFps(object sender, RoutedEventArgs args)
        {
            ShowFps = !ShowFps;
            Settings.Default.ShowFps = ShowFps;
            Settings.Default.Save();
        }

        /// <summary>
        /// The OnSelectViewLayout event handler.
        /// Sets the layout type and adjusts the Grid rows in the MainWindow to
        /// hide or show the appropriate OpenGl windows.
        /// </summary>
        /// <param name="sender">The layout type to display (<c>null</c> to use saved setting).</param>
        /// <param name="args">The Grid containing the OpenGl windows.</param>
        private async void OnSelectViewLayout(object sender, RoutedEventArgs args)
        {
            var layout = ((MenuItem)sender)?.Tag;
            if (layout != null)
            {
                // User has selected a new layout.
                SelectedLayout = (LayoutOptions)layout;
                Settings.Default.LayoutType = SelectedLayout;
                Settings.Default.Save();
            }
            else
            {
                // Called during initialization, so restore last selection.
                SelectedLayout = Settings.Default.LayoutType;
            }

            // Adjust the row heights of the layout grid based on the selection type.
            switch (Settings.Default.LayoutType)
            {
                case LayoutOptions.Panoramic:
                    GridContent.RowDefinitions[1].MinHeight = 0;
                    GridContent.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                    GridContent.RowDefinitions[2].Height = new GridLength(0, GridUnitType.Pixel);
                    break;
                case LayoutOptions.Immersive:
                    GridContent.RowDefinitions[1].MinHeight = 0;
                    GridContent.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
                    GridContent.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
                    break;
                case LayoutOptions.Panomersive:
                    GridContent.RowDefinitions[1].MinHeight = 0;
                    GridContent.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                    GridContent.RowDefinitions[2].Height = new GridLength(2, GridUnitType.Star);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Refresh the views with the newly configured layout.
            await Application.Current.Dispatcher.InvokeAsync(GridContent.InvalidateVisual);
            if (_sessionManager != null)
                await Application.Current.Dispatcher.InvokeAsync(_sessionManager.RefreshViews);
        }

        /// <summary>
        /// The OnWindowClosing event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The CancelEventArgs.</param>
        private void OnWindowClosing(object sender, CancelEventArgs args)
        {
            Dispose();
        }

        /// <summary>
        /// The OnWindowLoaded event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The RoutedEventArgs.</param>
        private void OnWindowLoaded(object sender, RoutedEventArgs args)
        {
            SelectFixedSource(CameraStreamType.Optera360);
        }

        /// <summary>
        /// The OnWindowSizeChanged event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="args">The SizeChangedEventArgs.</param>
        private async void OnWindowSizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (_sessionManager != null)
                await Application.Current.Dispatcher.InvokeAsync(_sessionManager.RefreshViews);
        }

        /// <summary>
        /// Initializes a fixed image camera source based on the selected camera source type/path.
        /// </summary>
        /// <param name="sourceType">The camera stream source type.</param>
        public void SelectFixedSource(CameraStreamType sourceType)
        {
            //try
            //{
                var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                var sourcePath = $"{currentPath}\\Resources\\";
                if (sourceType == CameraStreamType.Custom)
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.SelectedPath = sourcePath;
                        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                            return;

                        sourcePath = dialog.SelectedPath;
                        WindowTitle = "Pelco Camera Viewer - Custom Fixed Source Demo";
                    }
                }
                else
                {
                    switch (sourceType)
                    {
                        case CameraStreamType.Optera180:
                            sourcePath += "180";
                            WindowTitle = "Pelco Camera Viewer - Optera 180° Demo";
                            break;
                        case CameraStreamType.Optera270:
                            sourcePath += "270";
                            WindowTitle = "Pelco Camera Viewer - Optera 270° Demo";
                            break;
                        case CameraStreamType.Optera360:
                            sourcePath += "360";
                            WindowTitle = "Pelco Camera Viewer - Optera 360° Demo";
                            break;
                        case CameraStreamType.Full360:
                            sourcePath += "NissiBeach2";
                            WindowTitle = "Pelco Camera Viewer - Full 360° Demo";
                            break;
                    }
                }

                _sessionManager?.Dispose();
                _sessionManager = new SessionManager(ImmersiveWindow, PanoramicWindow);
                _sessionManager.LoadTestFiles(sourcePath);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //    Application.Current.Shutdown();
            //}
        }
    }
}
