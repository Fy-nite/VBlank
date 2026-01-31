using System;
using Microsoft.Xna.Framework;
using GfxCanvas = Adamantite.GFX.Canvas;

namespace StarChart.stdlib.W11
{
    // Visualizes a source pixel buffer by mapping it into a W11 window canvas.
    public class SurfaceMapWindow
    {
        readonly Window _window;
        readonly int _step;
        double _accum;

        public Window Window => _window;

        public SurfaceMapWindow(DisplayServer server, int x, int y, GfxCanvas source, int step = 1, int scale = 1)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (source == null) throw new ArgumentNullException(nameof(source));

            _step = Math.Max(1, step);
            int w = Math.Max(1, source.width / _step);
            int h = Math.Max(1, source.height / _step);

            _window = server.CreateWindow("surface_map", "Surface Map", new WindowGeometry(x, y, w, h), WindowStyle.Titled, scale);
            _window.Map();
        }

        public void Update(double deltaTime, GfxCanvas source)
        {
            if (source == null) return;
            _accum += deltaTime;
            if (_accum < 0.2) return;
            _accum = 0;

            var canvas = _window.Canvas;
            int w = canvas.Width;
            int h = canvas.Height;

            for (int y = 0; y < h; y++)
            {
                int sy = y * _step;
                if (sy >= source.height) break;
                for (int x = 0; x < w; x++)
                {
                    int sx = x * _step;
                    if (sx >= source.width) break;
                    var c = source.PixelData[sy * source.width + sx];
                    uint argb = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                    canvas.SetPixel(x, y, argb);
                }
            }
        }
    }
}
