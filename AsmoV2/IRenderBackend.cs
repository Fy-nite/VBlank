using System;
using System.Collections.Generic;
using VBlank.Abstractions;

namespace VBlank
{
    // Engine-facing IRenderBackend that forwards to the abstraction in the shared
    // VBlank.Abstractions project. The engine compiles against this interface so
    // it remains stable while concrete backends live in other projects.
    public partial interface IRenderBackend : IDisposable
    {
        void Initialize(object engine, object canvas);
        void Upload(object canvas, List<Rect> regions);
        void Present();
        bool PumpEvents();
    }
}
