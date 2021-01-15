using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;
using PanomersiveViewerNET.Models;
using PanomersiveViewerNET.Utils;
using ImExTkNet;

namespace PanomersiveViewerNET
{
    public class StreamSession : IDisposable
    {
        private unsafe byte** _data;
        private int[] _dataSizes;
        private readonly object _displayLock = new object();
        private readonly object _frameLock = new object();
        private IntPtr _lockCallback;
        private IntPtr _displayCallback;
        private IntPtr _formatCallback;
        private readonly List<Delegate> _callbacks = new List<Delegate>();
        private readonly string _endpoint;
        private FrameModel _frameModel;
        private readonly IntPtr _vlcLib;
        private IntPtr _mediaPlayer;
        private IntPtr _media;

        public CubeFace Face { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Layout { get; set; }
        public BitmapImage Image { get; set; }

        public StreamSession(CubeFace face, IntPtr libVlc, string endpoint, string layout, int height, int width)
        {
            Face = face;
            Layout = layout;
            Width = width;
            Height = height;
            _endpoint = endpoint;
            _vlcLib = libVlc;
        }

        public StreamSession(CubeFace face, string layout, BitmapImage image, int width, int height)
        {
            Face = face;
            Width = width;
            Height = height;
            Layout = layout;
            Image = image;
        }

        public unsafe void Play()
        {
            Imports.LockEventHandler leh = OnLock;
            Imports.DisplayEventHandler deh = OnDisplay;
            Imports.VideoFormatCallback formatCallback = OnFormatCallback;

            _formatCallback = Marshal.GetFunctionPointerForDelegate(formatCallback);
            _lockCallback = Marshal.GetFunctionPointerForDelegate(leh);
            _displayCallback = Marshal.GetFunctionPointerForDelegate(deh);

            _callbacks.Add(leh);
            _callbacks.Add(deh);
            _callbacks.Add(formatCallback);

            _mediaPlayer = Imports.libvlc_media_player_new(_vlcLib);
            Imports.libvlc_video_set_format_callbacks(_mediaPlayer, _formatCallback, IntPtr.Zero);
            Imports.libvlc_video_set_callbacks(_mediaPlayer, _lockCallback, IntPtr.Zero, _displayCallback, IntPtr.Zero);

            _media = Imports.libvlc_media_new_location(_vlcLib, Encoding.UTF8.GetBytes(_endpoint));
            Imports.libvlc_media_player_set_media(_mediaPlayer, _media);
            Imports.libvlc_media_player_play(_mediaPlayer);
        }

        private unsafe int OnFormatCallback(void** opaque, char* chroma, int* width, int* height, int* pitches, int* lines)
        {
            var pChroma = new IntPtr(chroma);
            Marshal.Copy(Encoding.UTF8.GetBytes("I420"), 0, pChroma, 4);
            *width = Width;
            *height = Height;

            pitches[0] = Width;
            pitches[1] = Width / 2;
            pitches[2] = Width / 2;

            lines[0] = Height;
            lines[1] = Height / 2;
            lines[2] = Height / 2;

            _dataSizes = new int[3];
            _dataSizes[0] = Width * Height;
            _dataSizes[1] = Width * Height / 4;
            _dataSizes[2] = Width * Height / 4;

            _data = (byte**)Imports.HeapAlloc(Imports.GetProcessHeap(), 0x00000008, new UIntPtr((uint)(sizeof(byte*) * _dataSizes.Length)));
            for (var i = 0; i < _dataSizes.Length; i++)
                _data[i] = (byte*)Imports.HeapAlloc(Imports.GetProcessHeap(), 0x00000008, new UIntPtr((uint)(sizeof(byte) * _dataSizes[i])));

            return 3;
        }

        private unsafe void* OnLock(void* opaque, void** plane)
        {
            for (var i = 0; i < _dataSizes.Length; i++)
                plane[i] = _data[i];

            return null;
        }

        private unsafe void OnDisplay(void* opaque, void* picture)
        {
            lock (_displayLock)
            {
                try
                {
                    var planes = new IntPtr[3];
                    for (var i = 0; i < _dataSizes.Length; i++)
                        planes[i] = new IntPtr(_data[i]);

                    Enqueue(new FrameModel(planes, _dataSizes));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public FrameModel Enqueue(FrameModel newValue)
        {
            lock (_frameLock)
            {
                var oldValue = _frameModel;
                _frameModel = newValue;
                return oldValue;
            }
        }

        public FrameModel Dequeue()
        {
            lock (_frameLock)
            {
                var oldValue = _frameModel;
                _frameModel = null;
                return oldValue;
            }
        }

        public void Dispose()
        {
            if (IntPtr.Zero != _mediaPlayer)
            {
                Imports.libvlc_video_set_callbacks(_mediaPlayer, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Imports.libvlc_media_player_stop(_mediaPlayer);
                Imports.libvlc_media_player_release(_mediaPlayer);
                Imports.libvlc_media_release(_media);
            }

            if (_dataSizes?.Length > 0)
            {
                unsafe
                {
                    for (var i = 0; i < _dataSizes.Length; i++)
                        Imports.HeapFree(Imports.GetProcessHeap(), 0, _data[i]);

                    Imports.HeapFree(Imports.GetProcessHeap(), 0, _data);
                }
            }

            _callbacks.Clear();
        }
    }
}
