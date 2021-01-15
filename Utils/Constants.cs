using System.Collections.Generic;
using ImExTkNet;

namespace PanomersiveViewerNET.Utils
{
    public class Constants
    {
        public static List<KeyValuePair<CubeFace, string>> TestFileInfo = new List<KeyValuePair<CubeFace, string>>
        {
            new KeyValuePair<CubeFace, string>(CubeFace.Left, "negx"),
            new KeyValuePair<CubeFace, string>(CubeFace.Front, "posx"),
            new KeyValuePair<CubeFace, string>(CubeFace.Down, "negy"),
            new KeyValuePair<CubeFace, string>(CubeFace.Back, "posy"),
            new KeyValuePair<CubeFace, string>(CubeFace.Up, "negz"),
            new KeyValuePair<CubeFace, string>(CubeFace.Right, "posz")
        };
    }
}
