using System;
using Adamantite.GFX;

namespace AsmoV2
{
    // Minimal interface for pluggable render backends. Implementations can be added
    // under conditional compilation or in separate packages to avoid forcing new deps.
    public interface IRenderBackend : IDisposable
    {
        // Initialize backend with the engine and the pixel Canvas that will be presented.
        void Initialize(AsmoGameEngine engine, Canvas canvas);

        // Called once per frame to present the canvas to the screen.
        void Present();

        // Request backend to handle any pending events; return false to indicate the app should exit.
        bool PumpEvents();
    }
}
