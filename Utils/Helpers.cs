using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using PanomersiveViewerNET.MediaService;
using PanomersiveViewerNET.Models;
using PanomersiveViewerNET.Properties;
using ImExTkNet;
using Microsoft.Win32;

namespace PanomersiveViewerNET.Utils
{
    class Helpers
    {
        public static CubeFace GetFaceFromEndpoint(string endpoint)
        {
            var face = CubeFace.Illegal;

            if (endpoint.ToLowerInvariant().Contains("back"))
                face = CubeFace.Back;
            else if (endpoint.ToLowerInvariant().Contains("down"))
                face = CubeFace.Down;
            else if (endpoint.ToLowerInvariant().Contains("front"))
                face = CubeFace.Front;
            else if (endpoint.ToLowerInvariant().Contains("left"))
                face = CubeFace.Left;
            else if (endpoint.ToLowerInvariant().Contains("mosaic"))
                face = CubeFace.Mosaic;
            else if (endpoint.ToLowerInvariant().Contains("unistream"))
                face = CubeFace.Mosaic;
            else if (endpoint.ToLowerInvariant().Contains("right"))
                face = CubeFace.Right;
            else if (endpoint.ToLowerInvariant().Contains("up"))
                face = CubeFace.Up;

            return face;
        }

        public static CameraStreamType GetCameraType(List<StreamSession> streams)
        {
            switch (streams.Count)
            {
                case 1:
                    if (streams[0].Width == 1280 && streams[0].Height == 1288 || streams[0].Width == 4064 && streams[0].Height == 4096)
                        return CameraStreamType.Optera180;

                    if (streams[0].Width == 864 && streams[0].Height == 2680 || streams[0].Width == 2144 && streams[0].Height == 6656)
                        return CameraStreamType.Optera270;

                    if (streams[0].Width == 800 && streams[0].Height == 2680 || streams[0].Width == 2048 && streams[0].Height == 6880)
                        return CameraStreamType.Optera360;

                    break;
                case 2:
                    return CameraStreamType.Optera180;
                case 4:
                    return CameraStreamType.Optera270;
                case 5:
                    return CameraStreamType.Optera360;
            }

            return CameraStreamType.Unknown;
        }
        
        public static CameraStreamType GetCameraInfo(CameraModel model, List<StreamSession> streams, IntPtr hMediaLib)
        {
            ServicePointManager.Expect100Continue = false;
            var messageElement = new TextMessageEncodingBindingElement { MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None) };
            var httpBinding = new HttpTransportBindingElement { AuthenticationScheme = AuthenticationSchemes.Digest };

            var bind = new CustomBinding(messageElement, httpBinding);
            var mediaClient = new MediaClient(bind, new EndpointAddress($"http://{model.IpAddress}/onvif/media_service"));
            if (mediaClient.ClientCredentials != null && !string.IsNullOrEmpty(model.Username) && !string.IsNullOrEmpty(model.Password))
            {
                mediaClient.ClientCredentials.HttpDigest.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                mediaClient.ClientCredentials.HttpDigest.ClientCredential.UserName = model.Username;
                mediaClient.ClientCredentials.HttpDigest.ClientCredential.Password = model.Password;
            }

            var configs = mediaClient.GetVideoEncoderConfigurations();
            var isUnistream = configs.Any(config => config.Name.ToLowerInvariant().Contains("unistream"));

            var url = $"rtsp://{model.IpAddress}/mosaic";
            if (model.UseHighResStream)
                url = isUnistream == false ? $"rtsp://{model.IpAddress}/immersive" : $"rtsp://{model.IpAddress}/unistream_v";
            
            // DESCRIBE call to send to the device
            var textToSend = $"DESCRIBE {url} RTSP/1.0\r\n"
                             + "CSeq: 1\r\n"
                             + "User-Agent: Pelco Example Client\r\n"
                             + "Accept: application/sdp\r\n\r\n";

            // Create a TCPClient object at the IP and port no.
            var client = new TcpClient(model.IpAddress, 554);
            var nwStream = client.GetStream();
            var bytesToSend = Encoding.ASCII.GetBytes(textToSend);

            // Send the DESCRIBE call
            nwStream.Write(bytesToSend, 0, bytesToSend.Length);

            // Read back the response
            var bytesToRead = new byte[client.ReceiveBufferSize];
            var bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
            var response = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
            client.Close();

            var lines = response.Split('\n');
            string videoLayoutMetadata = null;
            string videoUrlSubStr = null;
            var foundMedia = false;
            string path = null;
            foreach (var responseLine in lines)
            {
                var line = responseLine.Replace("\r", "").Replace("\n", "");
                var items = line.Split('=');
                if (items.Length < 2)
                    continue;

                var key = items[0];
                var val = items[1];
                if (key == "m" && val.StartsWith("video"))
                {
                    videoUrlSubStr = null;
                    videoLayoutMetadata = null;
                    foundMedia = true;
                    continue;
                }

                if (!foundMedia)
                    continue;

                // parse additional associated lines until we run out or
                // hit the next "m=" line
                if (key == "a")
                {
                    var attrs = val.Split(':');
                    if (attrs.Length < 2)
                        continue;

                    // parse attribute
                    var attrKey = attrs[0];
                    var attrVal = attrs[1];
                    if (attrKey == "x-pelco-video-layout")
                    {
                        videoLayoutMetadata = attrVal;
                        continue;
                    }

                    if (isUnistream)
                    {
                        videoUrlSubStr = url;
                        path = videoUrlSubStr.Split('/').Last();
                    }
                    else if (attrKey == "control")
                    {
                        videoUrlSubStr = attrVal;
                        if (videoUrlSubStr == "video")
                        {
                            videoUrlSubStr = $"rtsp://{model.IpAddress}/mosaic";
                            path = "mosaic";
                        }
                        else
                        {
                            var pos = videoUrlSubStr.IndexOf("_video", 0, StringComparison.Ordinal);
                            path = videoUrlSubStr.Substring(0, pos + 2);
                            videoUrlSubStr = $"rtsp://{model.IpAddress}/{path}";
                        }

                        continue;
                    }
                }

                // If we have both pieces of information then add to our list
                if (string.IsNullOrEmpty(videoUrlSubStr) || string.IsNullOrEmpty(videoLayoutMetadata))
                    continue;

                var config = configs.FirstOrDefault(cfg => cfg.Name.ToLowerInvariant() == path?.ToLowerInvariant());
                var height = config?.Resolution?.Height ?? 0;
                var width = config?.Resolution?.Width ?? 0;

                var stream = new StreamSession(Helpers.GetFaceFromEndpoint(videoUrlSubStr), hMediaLib, videoUrlSubStr, videoLayoutMetadata, height, width);
                streams.Add(stream);

                videoUrlSubStr = null;
                videoLayoutMetadata = null;
                foundMedia = false;
            }

            return GetCameraType(streams);
        }

        public static void SetPtzLimitOptions(ViewGenerator immersiveView, ViewGenerator panoramicView)
        {
            immersiveView.PtzLimitOptions = new ViewGenerator.PTZLimitOptions(Settings.Default.PtzLimitMode, Settings.Default.AutoZoomIn, Settings.Default.AutoZoomOut, Settings.Default.AutoPanTilt);
            panoramicView.PtzLimitOptions = new ViewGenerator.PTZLimitOptions(Settings.Default.PtzLimitMode, Settings.Default.AutoZoomIn, Settings.Default.AutoZoomOut, Settings.Default.AutoPanTilt);
        }

        public static int GetMercatorWidth(Context context, ViewGenerator panoramicView)
        {
            var dataBounds = context.DataBounds;

            // Get the data boundaries from the context, and map edge points into view space of the panoramic view.
            if (Math.Abs(dataBounds.PanStartRadians - dataBounds.PanEndRadians) < 0)
                return 0;

            // Asking for the view points right on the edge might fail due to rounding errors.
            // Backing off a little will still get us an accurate aspect ratio without risk
            // of falling off the edge.
            dataBounds.PanStartRadians += 0.001f;
            dataBounds.PanEndRadians -= 0.001f;

            // For 360, this may have changed the pan value.
            var viewAngle = panoramicView.ViewAngle;
            dataBounds.PanStartRadians += viewAngle.Pan;
            dataBounds.PanEndRadians -= viewAngle.Pan;

            var coords = context.MapSphericalCoordinates(dataBounds.PanStartRadians, 0, dataBounds.OriginPanRadians, dataBounds.OriginTiltRadians);
            panoramicView.SphericalToView(coords.PanRadians, coords.TiltRadians, out var left, out _);

            coords = context.MapSphericalCoordinates(dataBounds.PanEndRadians, 0, dataBounds.OriginPanRadians, dataBounds.OriginTiltRadians);
            panoramicView.SphericalToView(coords.PanRadians, coords.TiltRadians, out var right, out _);

            // Calculate the aspect ratio and use that to move the divider so
            // that the panoramic size is minimized.
            return right - left;
        }
    }
}
