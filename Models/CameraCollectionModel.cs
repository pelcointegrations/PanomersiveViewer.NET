using System.Collections.Generic;
using System.Configuration;
using System.Runtime.Serialization;

namespace PanomersiveViewerNET.Models
{
    /// <summary>
    /// The CameraModel class contains camera info entered by the user.
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CameraModel
    {        
        /// <summary>
        /// Gets the name to display for this camera.
        /// </summary>
        public string DisplayName { get => string.IsNullOrEmpty(FriendlyName) ? IpAddress : $"{FriendlyName} [{IpAddress}]"; }

        /// <summary>
        /// Gets or sets the camera friendly name.
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// Gets or sets the camera IP address.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the password for the camera.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets whether the high res. stream should be used for this camera.
        /// </summary>
        public bool UseHighResStream { get; set; }

        /// <summary>
        /// Gets or sets the username for the camera.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="CameraModel"/>.
        /// </summary>
        public CameraModel()
        {
            FriendlyName = string.Empty;
            IpAddress = string.Empty;
            Password = string.Empty;
            UseHighResStream = false;
            Username = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CameraModel"/>.
        /// </summary>
        /// <param name="ipAddress">The camera IP address.</param>
        /// <param name="friendlyName">The camera friendly name.</param>
        /// <param name="username">The username for the camera.</param>
        /// <param name="password">The password for the camera.</param>
        /// <param name="useHighResStream"><c>true</c> to use the high res. stream, otherwise <c>false</c>.</param>
        public CameraModel(string ipAddress, string friendlyName, string username, string password, bool useHighResStream)
        {
            FriendlyName = friendlyName ?? string.Empty;
            IpAddress = ipAddress ?? string.Empty;
            Username = username ?? string.Empty;
            Password = password ?? string.Empty;
            UseHighResStream = useHighResStream;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CameraModel"/>.
        /// </summary>
        /// <param name="info">The SerializationInfo.</param>
        /// <param name="context">The StreamingContext.</param>
        public CameraModel(SerializationInfo info, StreamingContext context)
        {
            FriendlyName = info.GetString("FriendlyName") ?? string.Empty;
            IpAddress = info.GetString("IpAddress") ?? string.Empty;
            Password = info.GetString("Password") ?? string.Empty;
            UseHighResStream = info.GetBoolean("UseHighResStream");
            Username = info.GetString("Username") ?? string.Empty;
        }
    }

    /// <summary>
    /// The CameraCollectionModel class contains the list of cameras added by the user.
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CameraCollectionModel
    {
        /// <summary>
        /// Gets or sets the list of cameras.
        /// </summary>
        public List<CameraModel> Cameras { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="CameraCollectionModel"/>.
        /// </summary>
        public CameraCollectionModel()
        {
            Cameras = new List<CameraModel>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CameraCollectionModel"/>.
        /// </summary>
        /// <param name="info">The SerializationInfo.</param>
        /// <param name="context">The StreamingContext.</param>
        public CameraCollectionModel(SerializationInfo info, StreamingContext context)
        {
            Cameras = (List<CameraModel>)info.GetValue("CameraModel", typeof(List<CameraModel>));
        }
    }
}
