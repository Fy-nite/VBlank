using System;
using System.Runtime.InteropServices;

namespace VBlank.Interop
{
    public class SDLRendererNative : IDisposable
    {
        private IntPtr handle;
        private const string DllName = "SDLRenderer";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr renderer_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_destroy(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool renderer_init(IntPtr h, int width, int height, string title);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_begin_frame(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_end_frame(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_clear(IntPtr h, float r, float g, float b, float a);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_shutdown(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void renderer_present_pixels(IntPtr h, IntPtr pixels, int width, int height);

        public SDLRendererNative()
        {
            handle = renderer_create();
            if (handle == IntPtr.Zero) throw new InvalidOperationException("Failed to create native renderer.");
        }

        public bool Init(int width, int height, string title) => renderer_init(handle, width, height, title);
        public void BeginFrame() => renderer_begin_frame(handle);
        public void EndFrame() => renderer_end_frame(handle);
        public void Clear(float r, float g, float b, float a) => renderer_clear(handle, r, g, b, a);
        public void Shutdown() => renderer_shutdown(handle);

        public void Present(VBlank.GPU.Surface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var arr = surface.Pixels;
            var handleG = System.Runtime.InteropServices.GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                renderer_present_pixels(handle, handleG.AddrOfPinnedObject(), surface.Width, surface.Height);
            }
            finally
            {
                handleG.Free();
            }
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                renderer_destroy(handle);
                handle = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~SDLRendererNative() => Dispose();
    }
}
