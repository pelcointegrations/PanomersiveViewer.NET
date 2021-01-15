using System;

namespace PanomersiveViewerNET.Utils
{
    /// <summary>
    /// The camera types that can be displayed.
    /// </summary>
    public enum CameraStreamType
    {
        /// <summary>
        /// An unknown camera type.
        /// </summary>
        Unknown,

        /// <summary>
        /// An Optera 180° camera.
        /// </summary>
        Optera180,

        /// <summary>
        /// An Optera 270° camera.
        /// </summary>
        Optera270,

        /// <summary>
        /// An Optera 360° camera.
        /// </summary>
        Optera360,

        /// <summary>
        /// A full 360° camera type.
        /// </summary>
        Full360,

        /// <summary>
        /// A custom camera type.
        /// </summary>
        Custom
    }

    /// <summary>
    /// The X/Y coordinates for ViewBox polygons.
    /// </summary>
    public struct PolygonPoint
    {
        /// <summary>
        /// The X coordinate.
        /// </summary>
        public int X;

        /// <summary>
        /// The Y coordinate.
        /// </summary>
        public int Y;
    }

    /// <summary>
    /// The layout options for displaying video.
    /// </summary>
    [Serializable]
    public enum LayoutOptions
    {
        /// <summary>
        /// The panoramic (mercator) only view.
        /// </summary>
        Panoramic,

        /// <summary>
        /// The immersive only view.
        /// </summary>
        Immersive,

        /// <summary>
        /// The panoramic over immersive view.
        /// </summary>
        Panomersive
    }
}
