using System;

namespace PanomersiveViewerNET.Models
{
    /// <summary>
    /// The FrameModel class holds the planes of a frame and their size.
    /// </summary>
    public class FrameModel
    {
        /// <summary>
        /// Gets or sets the planes pointer array.
        /// </summary>
        public IntPtr[] Planes { get; set; }

        /// <summary>
        /// Gets or sets the plane sizes array.
        /// </summary>
        public int[] Sizes { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="FrameModel"/>.
        /// </summary>
        /// <param name="planes">The planes pointer array.</param>
        /// <param name="sizes">The plane sizes array.</param>
        public FrameModel(IntPtr[] planes, int[] sizes)
        {
            Planes = planes;
            Sizes = sizes;
        }
    }
}
