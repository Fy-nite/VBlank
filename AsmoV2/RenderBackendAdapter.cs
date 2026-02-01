using System;
using System.Collections.Generic;
using VBlank.Abstractions;
using Adamantite.GPU;
using Adamantite.GFX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AsmoV2
{
    // Adapter that implements the engine IRenderBackend by delegating to
    // concrete backends available in the Adamantite project. This keeps the
    // engine decoupled from the concrete implementations via the shared
    // VBlank.Abstractions definitions.
    public class RenderBackendAdapter : IRenderBackend
    {
        private SDLAdapterRenderer? _sdlImpl;
        private Adamantite.GPU.MonoGameRenderBackend? _mgImpl;
        private bool _isSdl;

        public void Initialize(object engine, object canvas)
        {
            try { var v = Environment.GetEnvironmentVariable("ASMO_BACKEND"); _isSdl = !string.IsNullOrEmpty(v) && v.Equals("SDL", StringComparison.OrdinalIgnoreCase); } catch { _isSdl = false; }

            if (_isSdl)
            {
                if (canvas is Adamantite.GFX.Canvas c)
                {
                    var surf = new Adamantite.GPU.Surface(c.width, c.height);
                    _sdlImpl = new SDLAdapterRenderer(surf, (engine as AsmoGameEngine)?.Window?.Title ?? "VBlank SDL");
                    _sdlImpl.Upload(c, new System.Collections.Generic.List<VBlank.Abstractions.Rect>());
                }
            }
            else
            {
                // MonoGame backend: use GraphicsDevice from the engine
                if (engine is AsmoGameEngine ge && canvas is Adamantite.GFX.Canvas cc)
                {
                    _mgImpl = new Adamantite.GPU.MonoGameRenderBackend();
                    _mgImpl.Initialize(ge.GraphicsDevice, cc);
                }
            }
        }

        public void Upload(object canvas, List<Rect> regions)
        {
            if (_isSdl && _sdlImpl != null && canvas is Adamantite.GFX.Canvas c)
            {
                _sdlImpl.Upload(c, new System.Collections.Generic.List<VBlank.Abstractions.Rect>());
            }
            else if (_mgImpl != null && canvas is Adamantite.GFX.Canvas cmg)
            {
                _mgImpl.Upload(cmg, null!);
            }
        }

        public void Present()
        {
            if (_isSdl && _sdlImpl != null)
            {
                _sdlImpl.Submit();
            }
            else if (_mgImpl != null)
            {
                // No-op: MonoGame backend expects the engine to draw using its own SpriteBatch.
            }
        }

        public bool PumpEvents() => true;

        public void Dispose()
        {
            try { _sdlImpl?.Dispose(); } catch { }
            try { _mgImpl?.Dispose(); } catch { }
        }
    }
}
