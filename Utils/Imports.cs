using System;
using System.Runtime.InteropServices;

namespace PanomersiveViewerNET.Utils
{
    public static unsafe class Imports
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void* LockEventHandler(void* opaque, void** plane);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DisplayEventHandler(void* opaque, void* picture);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int VideoFormatCallback(void** opaque, char* chroma, int* width, int* height, int* pitches, int* lines);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvlc_media_player_new(IntPtr p_libvlc_instance);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_media_player_release(IntPtr libvlc_mediaplayer);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_media_player_set_media(IntPtr libvlc_media_player_t, IntPtr libvlc_media_t);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_media_player_play(IntPtr libvlc_mediaplayer);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_media_player_stop(IntPtr libvlc_mediaplayer);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_video_set_callbacks(IntPtr mp, IntPtr @lock, IntPtr unlock, IntPtr display, IntPtr opaque);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_video_set_format_callbacks(IntPtr p_mi, IntPtr setup, IntPtr cleanup);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvlc_media_new_location(IntPtr p_instance, [MarshalAs(UnmanagedType.LPArray)] byte[] psz_mrl);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_media_release(IntPtr libvlc_media_inst);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvlc_new(int argc, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] argv);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_release(IntPtr libvlc_instance_t);

        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvlc_video_set_format(IntPtr mp, [MarshalAs(UnmanagedType.LPArray)] byte[] chroma, int width, int height, int pitch);

        [DllImport("Kernel32", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        public static extern void MoveMemory(void* dest, void* src, int size);

        [DllImport("kernel32")]
        public static extern void* HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport("kernel32")]
        public static extern bool HeapFree(IntPtr hHeap, uint flags, void* block);

        [DllImport("kernel32")]
        public static extern IntPtr GetProcessHeap();
    }
}
