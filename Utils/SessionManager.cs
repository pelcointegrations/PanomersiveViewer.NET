using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media.Imaging;
using PanomersiveViewerNET.Models;
using PanomersiveViewerNET.Properties;
using PanomersiveViewerNET.Utils;
using ImExTkNet;
using SystemColors = System.Drawing.SystemColors;

namespace PanomersiveViewerNET
{
    public class SessionManager : IDisposable
    {
        private readonly object _streamsLock;
        private IntPtr _libVlc;
        private readonly OpenGlWindow _immersiveWindow;
        private readonly OpenGlWindow _panoramicWindow;
        private VideoStream _videoStream;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _token;
        private Task _frameThread;
        private int _roundRobinIdx;
        private bool _doUpdate;
        private int _frameRate;
        private int _latestFps;
        private System.Timers.Timer _timer;

        public CameraStreamType CameraType { get; set; }
        public List<StreamSession> Streams { get; set; }
        public bool IsFixedImage { get; set; }
        public List<CubeFace> Faces { get => Streams.Select(stream => stream.Face).ToList(); }
        public List<int> Heights { get => Streams.Select(stream => stream.Height).ToList(); }
        public List<int> Widths { get => Streams.Select(stream => stream.Width).ToList(); }
        public List<string> Layouts { get => Streams.Select(stream => stream.Layout).ToList(); }
        public Context Context;
        public ViewGenerator ImmersiveView;
        public ViewGenerator PanoramicView;

        public SessionManager(OpenGlWindow immersiveWindow, OpenGlWindow panoramicWindow)
        {
            _immersiveWindow = immersiveWindow;
            _panoramicWindow = panoramicWindow;

            Streams = new List<StreamSession>();
            _streamsLock = new object();
        }

        #region Stream Initialization Methods

        public void LoadCameraStream(CameraModel model)
        {
            try
            {
                var options = new[] { "--aout=dummy", "--vout=dummy", "--clock-synchro=0", "--rtsp-frame-buffer-size=800000" };
                var currentPath = new DirectoryInfo(System.Windows.Forms.Application.ExecutablePath).Parent;
                var path = IntPtr.Size == 8 ? $"{currentPath?.FullName}\\libvlc\\win-x64\\" : $"{currentPath?.FullName}\\libvlc\\win-x86\\";
                if (!string.IsNullOrEmpty(path))
                    Directory.SetCurrentDirectory(path);

                _libVlc = Imports.libvlc_new(options.Length, options);

                CameraType = Helpers.GetCameraInfo(model, Streams, _libVlc);
                if (CameraType == CameraStreamType.Unknown)
                    return;

                _tokenSource = new CancellationTokenSource();
                _token = _tokenSource.Token;
                _frameThread = Task.Factory.StartNew(ProcessFrames, _token);
                foreach (var stream in Streams)
                    stream.Play();

                _timer = new System.Timers.Timer();
                _timer.Elapsed += Timer_Elapsed;
                _timer.Interval = 1000;
                _timer.Start();
                SetupVideo();
                RefreshViews();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
}

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _latestFps = _frameRate;
            _frameRate = 0;
        }

        public void LoadTestFiles(string path)
        {
            IsFixedImage = true;
            foreach (var stream in Constants.TestFileInfo)
            {
                // Load the image file
                BitmapImage img = null;
                try
                {
                    img = new BitmapImage(new Uri($"file:\\\\{path}\\{stream.Value}.jpg"));
                }
                catch
                {
                    // ignored
                }

                if (img == null || img.Width <= 0 || img.Height <= 0)
                    continue;

                // Read the whole layout model file into a string
                var layout = File.ReadAllText($"{path}\\{stream.Value}_layout.txt");
                if (string.IsNullOrEmpty(layout))
                    continue;

                lock (_streamsLock)
                {
                    Streams.Add(new StreamSession(stream.Key, layout, img, img.PixelWidth, img.PixelHeight));
                }
            }

            if (Streams.Count == 0)
                return;

            CameraType = Helpers.GetCameraType(Streams);
            SetupVideo();
            RefreshViews();
        }

        #endregion

        #region Core Video Methods

        public void SetupVideo()
        {
            if (Streams.Count == 0)
                return;

            _immersiveWindow.Use();
            var format = IsFixedImage ? StreamImageFormat.BGRA : StreamImageFormat.I420;
            Context = new Context(Settings.Default.StreamOptimizedType, format);
            if (Context == null)
                throw new Exception("Failed to create ImExTk context");

            if (_videoStream != null)
            {
                _videoStream.Dispose();
                _videoStream = null;
            }

            _videoStream = new VideoStream(Context.Id, Faces, Layouts, Widths, Heights, 2);
            ImmersiveView = new ViewGenerator(Context.Id, ViewType.Immersive);
            if (ImmersiveView == null)
            {
                throw new Exception("Failed to create view generator\n" + Context.LastErrorDetails);
            }

            ImmersiveView.ViewSize = new System.Drawing.Size(_immersiveWindow.Size.Width, _immersiveWindow.Size.Height);

            _panoramicWindow.Use();
            var viewType = ViewType.Mercator;
            if (CameraType == CameraStreamType.Optera270)
                viewType = ViewType.Optimized270;

            PanoramicView = new ViewGenerator(Context.Id, viewType);
            if (PanoramicView == null)
            {
                throw new Exception("Failed to create view generator\n" + Context.LastErrorDetails);
            }

            PanoramicView.ViewSize = new System.Drawing.Size(_panoramicWindow.Size.Width, _panoramicWindow.Size.Height);

            PanoramicView.LetterboxingColor = Color.FromArgb(40, 40, 40);

            Helpers.SetPtzLimitOptions(ImmersiveView, PanoramicView);
            AdjustCameraTilt(Settings.Default.CameraTiltAngle, Settings.Default.OverrideCameraTilt);
        }

        private async Task ProcessFrames()
        {
            while (!_token.IsCancellationRequested)
            {
                if (Streams == null || Streams.Count == 0)
                    continue;

                _roundRobinIdx = (_roundRobinIdx + 1) % Streams.Count;
                _doUpdate = _roundRobinIdx == Streams.Count - 1;
                var frame = Streams[_roundRobinIdx]?.Dequeue();
                if (frame != null)
                    await Application.Current.Dispatcher.InvokeAsync(() => RenderFrame(_roundRobinIdx, frame));
            }
        }

        public void RenderFrame(int index, FrameModel frameModel)
        {
            if (null == _immersiveWindow || Streams.Count == 0)
                return;

            var session = Streams[index];
            if (session == null)
                return;

            _immersiveWindow.Use();
            var buffers = _videoStream?.GetFrameBuffer(session.Face);
            if (buffers == null || IntPtr.Zero == buffers.ImageBRGABufferOrY || IntPtr.Zero == buffers.ImageU || IntPtr.Zero == buffers.ImageV)
                return;

            unsafe
            {
                Imports.MoveMemory(buffers.ImageBRGABufferOrY.ToPointer(), frameModel.Planes[0].ToPointer(), frameModel.Sizes[0]);
                Imports.MoveMemory(buffers.ImageU.ToPointer(), frameModel.Planes[1].ToPointer(), frameModel.Sizes[1]);
                Imports.MoveMemory(buffers.ImageV.ToPointer(), frameModel.Planes[2].ToPointer(), frameModel.Sizes[2]);
            }

            _videoStream?.ReleaseFrameBuffer(session.Face, buffers, true);

            if (!_doUpdate)
                return;

            _frameRate++;
            _doUpdate = false;
            _immersiveWindow.Use();
            DrawFps();
            ImmersiveView.RenderDirect();
            _immersiveWindow.SwapBuffers();

            _panoramicWindow.Use();
            PanoramicView.RenderDirect();
            _panoramicWindow.SwapBuffers();
        }

        public void RefreshViews()
        {
            if (ImmersiveView == null || _immersiveWindow.Width == 0 && _immersiveWindow.Height == 0 ||
                PanoramicView == null || _panoramicWindow.Width == 0 && _panoramicWindow.Height == 0)
                return;

            _immersiveWindow.Use();
            if (IsFixedImage)
            {
                foreach (var stream in Streams)
                {
                    // Copy image data to the stream
                    var pImgBuf = _videoStream.GetFrameBuffer(stream.Face);
                    stream.Image.CopyPixels(new Int32Rect(0, 0, stream.Image.PixelWidth, stream.Image.PixelHeight), pImgBuf.ImageBRGABufferOrY, stream.Image.PixelWidth * stream.Image.PixelHeight * 4, stream.Image.PixelWidth * 4);
                    _videoStream.ReleaseFrameBuffer(stream.Face, pImgBuf, true);
                }
            }

            _immersiveWindow.Use();
            ImmersiveView.ViewSize = new System.Drawing.Size(_immersiveWindow.Width, _immersiveWindow.Height);
            ImmersiveView.RenderDirect();
            _immersiveWindow.SwapBuffers();

            _panoramicWindow.Use();
            PanoramicView.ViewSize = new System.Drawing.Size(_panoramicWindow.Width, _panoramicWindow.Height);
            DrawViewBox();
            PanoramicView.RenderDirect();
            _panoramicWindow.SwapBuffers();
        }

        public void CleanupVideo()
        {
            _immersiveWindow.Use();
            if (ImmersiveView != null)
            {
                ImmersiveView.Dispose();
                ImmersiveView = null;
            }
            if (PanoramicView != null)
            {
                PanoramicView.Dispose();
                PanoramicView = null;
            }

            _videoStream?.Dispose();
            if (Context == null)
                return;

            Context.Dispose();
            Context = null;
        }

        #endregion

        #region PTZ Methods

        public async void MoveToPosition(System.Drawing.Point location, bool isPanoramic, bool doZoom = false)
        {
            var viewId = isPanoramic ? PanoramicView : ImmersiveView;
            var window = isPanoramic ? _panoramicWindow : _immersiveWindow;

            window.Use();
            if (viewId.ViewToSpherical(location.X, location.Y, out var panRadians, out var tiltRadians) != Result.NoError)
            {
                // Point is probably outside the viewing area -- ignore
                return;
            }

            if (Settings.Default.LayoutType != LayoutOptions.Panoramic)
            {
                DrawTarget((uint)location.X, (uint)location.Y, viewId, false);
                viewId.RenderDirect();
                window.SwapBuffers();
                Thread.Sleep(200);
            }

            // Rotate (and maybe zoom) all views to the P.O.I.
            _immersiveWindow.Use();
            ImmersiveView.ViewAngle = new ViewGenerator.ViewAngles(panRadians, tiltRadians);

            window.Use();
            DrawTarget((uint)location.X, (uint)location.Y, viewId, true);
            RefreshViews();

            if (!doZoom)
                return;

            var curZ = ImmersiveView.Zoom;
            
            // Adjust the zoom step size based on how far we are currently zoomed in.
            // Helps keep a consistent zoom adjustment amount
            var zoomStep = 100;
            if (curZ >= 0.2f && curZ <= 0.4f)
                zoomStep = 80;
            else if (curZ >= 0.4f && curZ <= 0.6f)
                zoomStep = 60;
            else if (curZ >= 0.6f && curZ <= 0.8f)
                zoomStep = 40;
            else if (curZ > 0.8f)
                zoomStep = 20;

            for (var i = 0; i < 10; i++)
                await Application.Current.Dispatcher.InvokeAsync(() => PerformZoom(location, zoomStep, isPanoramic));
        }

        public void AdjustCameraTilt(int cameraTiltAngle, bool overrideCameraTilt)
        {
            Context.IsCameraTiltCorrectionEnabled =  overrideCameraTilt;
            Context.CameraTiltCorrectionAngle = cameraTiltAngle * (float)3.14159265358979323846 / 180.0f;
            RefreshViews();
        }

        public void PerformPanTilt(System.Drawing.Point anchor, System.Drawing.Point newLocation, bool isPanoramic)
        {
            var viewId = isPanoramic ? PanoramicView : ImmersiveView;
            var viewAngle = viewId.ViewAngle;

            viewId.ViewToSpherical(anchor.X, anchor.Y, out var anchorPan, out var anchorTilt);

            viewId.ViewToSpherical(newLocation.X, newLocation.Y, out var mousePan, out var mouseTilt);

            // reverse sense to move the view instead of the data.
            viewId.ViewAngle = new ViewGenerator.ViewAngles(viewAngle.Pan - (mousePan - anchorPan), viewAngle.Tilt - (mouseTilt - anchorTilt));

            RefreshViews();
        }

        public void PerformZoom(System.Drawing.Point location, int delta, bool isPanoramic)
        {
            if (ImmersiveView == null)
                return;

            // Calculate the zoom delta (zoomSpeed is an empirically determined value).
            const float zoomSpeed = 0.02f; 
            const int wheelDelta = 120;
            var deltaZ = delta * zoomSpeed / wheelDelta;

            // Map the location into view absolute coordinates before zooming.
            ImmersiveView.ViewToSpherical(location.X, location.Y, out var panPreZoom, out var tiltPreZoom);

            // Get the current zoom level.
            var curZ = ImmersiveView.Zoom;

            // Calculate the new zoom level from the delta.
            var newZ = Math.Min(Math.Max(curZ + deltaZ, 0.0f), 1.0f);
            if (!(Math.Abs(newZ - curZ) > 0))
                return;

            // Set the new zoom level.
            ImmersiveView.Zoom = newZ;

            // In the Immersive view, we want to zoom while keeping the location at the same position within the view.
            if (!isPanoramic)
            {
                // Map the location into view absolute coordinates after zooming. (All points except the center of the view will move as a result of zooming.)
                if (ImmersiveView.ViewToSpherical(location.X, location.Y, out var panPostZoom, out var tiltPostZoom) == Result.NoError)
                {
                    var viewAngle = ImmersiveView.ViewAngle;
                    viewAngle.Pan -= panPostZoom - panPreZoom;
                    viewAngle.Tilt -= tiltPostZoom - tiltPreZoom;

                    // Override PTZ limiting options during this operation - we don't want any zooming side-effects here.
                    ImmersiveView.PtzLimitOptions = new ViewGenerator.PTZLimitOptions(Settings.Default.PtzLimitMode, false, false, Settings.Default.AutoPanTilt);

                    // Now pan/tilt the view by the amount that our target point moved, in order to bring that point back to where it was.
                    ImmersiveView.ViewAngle = viewAngle;

                    // Restore the PTZ limiting options
                    ImmersiveView.PtzLimitOptions = new ViewGenerator.PTZLimitOptions(Settings.Default.PtzLimitMode, Settings.Default.AutoZoomIn, Settings.Default.AutoZoomOut, Settings.Default.AutoPanTilt);
                }
            }

            RefreshViews();
        }

        #endregion

        #region Overlay Methods

        public void DrawViewBox()
        {
            if (Settings.Default.LayoutType != LayoutOptions.Panomersive)
                return;

            var panoramicViewSize = PanoramicView.ViewSize;
            var ImmersiveViewSize = ImmersiveView.ViewSize;

            // Draw zone on mercator using 3 points per 'side' for a total of 8 points
            // With 360, you can 'cross' from one side of the view to the other, so have an extra polygon
            var distanceTooFar = panoramicViewSize.Width / 2;
            const int kMaxNumPolyPts = 12;
            var polygon = new PolygonPoint[kMaxNumPolyPts];

            // Can still get errors.  In that case, skip that data point
            var numPtsInPoly = 0;
            var vtsResult = ImmersiveView.ViewToSpherical(0, 0, out var immersivePan, out var immersiveTilt);
            var stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width / 4, 0, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width / 2, 0, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width * 3 / 4, 0, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width - 1, 0, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width - 1, ImmersiveViewSize.Height / 2, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width - 1, ImmersiveViewSize.Height - 1, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(3 * ImmersiveViewSize.Width / 4, ImmersiveViewSize.Height - 1, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width / 2, ImmersiveViewSize.Height - 1, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(ImmersiveViewSize.Width / 4, ImmersiveViewSize.Height - 1, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(0, ImmersiveViewSize.Height - 1, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            vtsResult = ImmersiveView.ViewToSpherical(0, ImmersiveViewSize.Height / 2, out immersivePan, out immersiveTilt);
            stvResult = PanoramicView.SphericalToView(immersivePan, immersiveTilt, out polygon[numPtsInPoly].X, out polygon[numPtsInPoly].Y);
            if (vtsResult == Result.NoError && stvResult == Result.NoError)
                numPtsInPoly++;

            System.Drawing.Point[] polygonPoints;
            System.Drawing.Point[] polygonPoints2 = null;
            // For the coordinate adjust system, use the realWidth back from toolkit.
            //  For the rest, need to add any letter boxing size on the left
            var realWidth = Helpers.GetMercatorWidth(Context, PanoramicView);
            var letterBoxLeft = (panoramicViewSize.Width - realWidth) / 2;
            if (CameraType == CameraStreamType.Optera360)
            {
                if (realWidth <= 0)
                    return;

                var savedPolygon = new PolygonPoint[kMaxNumPolyPts];
                for (var i = 0; i < kMaxNumPolyPts; i++)
                {
                    savedPolygon[i] = polygon[i];
                }

                for (var i = 1; i < numPtsInPoly; i++)
                {
                    if (Math.Abs(polygon[0].X - polygon[i].X) > distanceTooFar)
                    {
                        // add width to polygon
                        if (polygon[0].X > polygon[i].X)
                            polygon[i].X += realWidth;
                        else
                            polygon[i].X -= realWidth;
                    }
                }

                // Now check for an over-boundary condition
                var indexOfFirstOver = 0;
                var indexOfSecondOver = 0;
                for (var i = 1; i < numPtsInPoly; i++)
                {
                    if (indexOfFirstOver == 0)
                    {
                        if (polygon[i].X > realWidth + letterBoxLeft || polygon[i].X < letterBoxLeft)
                        {
                            indexOfFirstOver = i;
                        }
                    }
                    else
                    {
                        if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                        {
                            if (polygon[i].X <= realWidth + letterBoxLeft)
                            {
                                indexOfSecondOver = i;
                                break;
                            }
                        }
                        else
                        {
                            if (polygon[i].X >= letterBoxLeft)
                            {
                                indexOfSecondOver = i;
                                break;
                            }
                        }
                    }
                }

                indexOfSecondOver = indexOfSecondOver == 0 ? numPtsInPoly : indexOfSecondOver;
                if (indexOfFirstOver != 0)
                {
                    // Find the y - intercept
                    var yAtTheEdge1 = 0.0;
                    if (polygon[indexOfFirstOver - 1].X == polygon[indexOfFirstOver].X)
                    {
                        // This should never happen
                    }
                    else
                    {
                        yAtTheEdge1 = ((double)polygon[indexOfFirstOver - 1].Y - polygon[indexOfFirstOver].Y) / ((double)polygon[indexOfFirstOver - 1].X - polygon[indexOfFirstOver].X);
                    }

                    if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                    {
                        yAtTheEdge1 *= realWidth + letterBoxLeft - polygon[indexOfFirstOver - 1].X;
                    }
                    else
                    {
                        // Be sure to keep algebra consistent
                        //  Since slope is found from index to index - 1 . . .
                        yAtTheEdge1 *= letterBoxLeft - polygon[indexOfFirstOver - 1].X;
                    }

                    yAtTheEdge1 += polygon[indexOfFirstOver - 1].Y;
                    yAtTheEdge1 = Math.Round(yAtTheEdge1);

                    var yAtTheEdge2 = 0.0;
                    if (polygon[indexOfSecondOver % numPtsInPoly].X == polygon[indexOfSecondOver - 1].X)
                    {
                        // This should never happen
                    }
                    else
                    {
                        yAtTheEdge2 = ((double)polygon[indexOfSecondOver % numPtsInPoly].Y - polygon[indexOfSecondOver - 1].Y) / ((double)polygon[indexOfSecondOver % numPtsInPoly].X - polygon[indexOfSecondOver - 1].X);
                    }

                    if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                    {
                        yAtTheEdge2 *= realWidth + letterBoxLeft - polygon[indexOfSecondOver % numPtsInPoly].X;
                    }
                    else
                    {
                        yAtTheEdge2 *= letterBoxLeft - polygon[indexOfSecondOver % numPtsInPoly].X;
                    }

                    yAtTheEdge2 += polygon[indexOfSecondOver % numPtsInPoly].Y;
                    yAtTheEdge2 = Math.Round(yAtTheEdge2);

                    // Need more than standard polygons because you could add another point
                    //  if only one point of the poly goes over the edge.
                    var polygonToSend = new PolygonPoint[kMaxNumPolyPts + 1];
                    for (var i = 0; i < indexOfFirstOver; i++)
                    {
                        polygonToSend[i] = polygon[i];
                    }

                    if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                    {
                        polygonToSend[indexOfFirstOver].X = realWidth + letterBoxLeft - 1;
                        polygonToSend[indexOfFirstOver + 1].X = realWidth + letterBoxLeft - 1;
                    }
                    else
                    {
                        polygonToSend[indexOfFirstOver].X = letterBoxLeft;
                        polygonToSend[indexOfFirstOver + 1].X = letterBoxLeft;
                    }

                    polygonToSend[indexOfFirstOver].Y = (int)yAtTheEdge1;
                    polygonToSend[indexOfFirstOver + 1].Y = (int)yAtTheEdge2;
                    var numPts = indexOfFirstOver + 2;
                    for (var i = indexOfSecondOver; i < numPtsInPoly; i++)
                    {
                        polygonToSend[numPts++] = polygon[i];
                    }

                    polygonPoints = new System.Drawing.Point[numPts];
                    for (var i = 0; i < numPts; i++)
                    {
                        polygonPoints[i].X = polygonToSend[i].X;
                        polygonPoints[i].Y = polygonToSend[i].Y;
                    }

                    // Now need to send the other side of the polygon
                    if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                    {
                        polygonToSend[0].X = letterBoxLeft;
                    }
                    else
                    {
                        polygonToSend[0].X = realWidth + letterBoxLeft - 1;
                    }

                    polygonToSend[0].Y = (int)yAtTheEdge1;
                    numPts = 1;
                    for (var i = indexOfFirstOver; i < indexOfSecondOver; i++)
                    {
                        polygonToSend[numPts++] = savedPolygon[i];
                    }

                    if (polygon[indexOfFirstOver].X > realWidth + letterBoxLeft)
                    {
                        polygonToSend[numPts].X = letterBoxLeft;
                    }
                    else
                    {
                        polygonToSend[numPts].X = realWidth + letterBoxLeft - 1;
                    }

                    polygonToSend[numPts].Y = (int)yAtTheEdge2;
                    numPts++;

                    polygonPoints2 = new System.Drawing.Point[numPts];
                    for (var i = 0; i < numPts; i++)
                    {
                        polygonPoints2[i].X = polygonToSend[i].X;
                        polygonPoints2[i].Y = polygonToSend[i].Y;
                    }
                }
                else
                {
                    polygonPoints = new System.Drawing.Point[numPtsInPoly];
                    for (var i = 0; i < numPtsInPoly; i++)
                    {
                        polygonPoints[i].X = polygon[i].X;
                        polygonPoints[i].Y = polygon[i].Y;
                    }
                }
            }
            else
            {
                polygonPoints = new System.Drawing.Point[numPtsInPoly];
                for (var i = 0; i < numPtsInPoly; i++)
                {
                    polygonPoints[i].X = polygon[i].X;
                    polygonPoints[i].Y = polygon[i].Y;
                }
            }

            var bitmap = new Bitmap(panoramicViewSize.Width, panoramicViewSize.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                var pen = new Pen(Color.White, 2);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw lines for the first ViewBox
                if (polygonPoints.Length > 1)
                {
                    for (var i = 0; i < polygonPoints.Length; i++)
                    {
                        var nextIndex = (i != polygonPoints.Length - 1) ? i + 1 : 0;
                        // Skip drawing a line if the next two points are on the edge of the view
                        if (CameraType == CameraStreamType.Optera360 && polygonPoints[i].X == polygonPoints[nextIndex].X &&
                            (polygonPoints[i].X >= letterBoxLeft + 1 || polygonPoints[i].X <= realWidth + letterBoxLeft - 1))
                            continue;

                        if (polygonPoints[i].X < 0 || polygonPoints[i].Y < 0 || polygonPoints[nextIndex].X < 0 || polygonPoints[nextIndex].Y < 0)
                            continue;

                        g.DrawLine(pen, polygonPoints[i].X, polygonPoints[i].Y, polygonPoints[nextIndex].X, polygonPoints[nextIndex].Y);
                    }
                }

                // Draw lines for the second ViewBox (spanning the edges of a 360 view)
                if (polygonPoints2 != null)
                {
                    for (var i = 0; i < polygonPoints2.Length; i++)
                    {
                        var nextIndex = (i != polygonPoints2.Length - 1) ? i + 1 : 0;

                        // Skip drawing a line if the next two points are on the edge of the view
                        if (CameraType == CameraStreamType.Optera360 && polygonPoints2[i].X == polygonPoints2[nextIndex].X &&
                            (polygonPoints2[i].X >= letterBoxLeft + 1 || polygonPoints2[i].X <= realWidth + letterBoxLeft - 1))
                            continue;

                        if (polygonPoints2[i].X < 0 || polygonPoints2[i].Y < 0 || polygonPoints2[nextIndex].X < 0 || polygonPoints2[nextIndex].Y < 0)
                            continue;

                        g.DrawLine(pen, polygonPoints2[i].X, polygonPoints2[i].Y, polygonPoints2[nextIndex].X, polygonPoints2[nextIndex].Y);
                    }
                }
            }

            if (Settings.Default.ShowFps && !IsFixedImage)
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Black)), 5, 5, 55, 22);
                    g.DrawString($"{_latestFps.ToString().PadLeft(2, '0')}", new Font(new FontFamily("Arial"), 16), new SolidBrush(Color.White), 5, 5);
                    g.DrawString("fps", new Font(new FontFamily("Arial"), 12), new SolidBrush(Color.White), 30, 8);
                }
            }

            var res = PanoramicView.AddBitmapToOverlay(bitmap);
            if (res != Result.NoError)
                Console.WriteLine("DrawTarget Error: " + res);
        }

        private void DrawFps()
        {
            ViewGenerator view = PanoramicView;
            if (Settings.Default.LayoutType == LayoutOptions.Immersive)
                view = ImmersiveView;

            var bitmap = new Bitmap(60, 30, PixelFormat.Format32bppArgb);
            if (Settings.Default.ShowFps)
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Black)), 5, 5, 55, 22);

                    var fontFamily = new FontFamily("Arial");

                    g.DrawString($"{_latestFps.ToString().PadLeft(2, '0')}", new Font(fontFamily, 16),
                        new SolidBrush(Color.White), 5, 5);
                    g.DrawString("fps", new Font(fontFamily, 12), new SolidBrush(Color.White), 30, 8);
                }
            }

            var res = view.AddBitmapToOverlay(bitmap);
            if (res != Result.NoError)
                Console.WriteLine("DrawTarget Error: " + res);
        }

        private void DrawTarget(uint xOffset, uint yOffset, ViewGenerator view, bool clearTarget)
        {
            var bitmap = new Bitmap(22, 22, PixelFormat.Format32bppArgb);
            if (!clearTarget)
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    var pen = new Pen(new SolidBrush(Color.FromArgb(170, Color.White)), 1) { DashPattern = new[] {4.0F, 3.0F, 4.0F, 3.0F, 4.0F} };
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    g.FillEllipse(new SolidBrush(Color.FromArgb(170, Color.LightSkyBlue)), 0, 0, 20, 20);
                    g.DrawLine(pen, 10, 1, 10, 19);
                    g.DrawLine(pen, 1, 10, 19, 10);
                }
            }

            var res = view.AddBitmapToOverlay(bitmap, xOffset - 10, yOffset - 10);
            if (res != Result.NoError)
                Console.WriteLine("DrawTarget Error: " + res);
        }

        #endregion

        #region IDisposable Method

        public void Dispose()
        {
            _tokenSource?.Cancel();
            _frameThread?.Wait(_token);
            foreach (var stream in Streams)
                stream.Dispose();

            Streams.Clear();
            _timer?.Dispose();
            if (_libVlc != IntPtr.Zero)
                Imports.libvlc_release(_libVlc);

            if (_frameThread != null)
            {
                _frameThread.Dispose();
                _frameThread = null;
            }

            if (_tokenSource != null)
            {
                _tokenSource.Dispose();
                _tokenSource = null;
            }

            CleanupVideo();
        }

        #endregion
    }
}
