using System;
using System.Collections.Generic;

namespace VBlank.Abstractions
{
    // Minimal abstraction for a render backend. This lives in a separate project
    // so both the engine and renderer implementations can reference it without
    // creating circular project dependencies.
    public struct Rect
    {
        public int X;
        public int Y;
        public int W;
        public int H;
    }

    public interface IRenderBackend : IDisposable
    {
        // Initialize backend with opaque engine and canvas objects.
        void Initialize(object engine, object canvas);

        // Upload pixel data (or dirty regions) from the canvas.
        void Upload(object canvas, List<Rect> regions);

        // Present the current frame to the screen.
        void Present();

        // Handle any pending events; return false to indicate the app should exit.
        bool PumpEvents();
    }
}
