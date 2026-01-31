using System;
using Microsoft.Xna.Framework;

namespace Adamantite.GFX
{
    public partial class Canvas
    {
        // Position of the canvas (optional)
        public int x;
        public int y;

        // Size of the canvas
        public int width;
        public int height;

        // Underlying pixel buffer using MonoGame's Color struct
        public Color[] PixelData { get; private set; }
        // Dirty rectangle tracking for partial texture uploads
        int _dirtyX;
        int _dirtyY;
        int _dirtyW;
        int _dirtyH;
        bool _dirty;

        public bool IsDirty => _dirty;
        public int DirtyX => _dirtyX;
        public int DirtyY => _dirtyY;
        public int DirtyWidth => _dirtyW;
        public int DirtyHeight => _dirtyH;

        public Canvas(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.x = 0;
            this.y = 0;
            PixelData = new Color[width * height];
            MarkDirtyRect(0, 0, width, height);
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < PixelData.Length; i++) PixelData[i] = c;
            MarkDirtyRect(0, 0, width, height);
        }

        public void SetPixel(int x, int y, Color c)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            PixelData[y * width + x] = c;
            MarkDirtyRect(x, y, 1, 1);
        }

        public void DrawFilledRect(int startX, int startY, int w, int h, Color c)
        {
            for (int yy = 0; yy < h; yy++)
            {
                int py = startY + yy;
                if (py < 0 || py >= height) continue;
                for (int xx = 0; xx < w; xx++)
                {
                    int px = startX + xx;
                    if (px < 0 || px >= width) continue;
                    SetPixel(px, py, c);
                }
            }
            MarkDirtyRect(startX, startY, w, h);
        }

        // Helper for converting packed 0xAARRGGBB into a MonoGame Color
        public static Color ColorFromLong(long value)
        {
            uint v = (uint)value;
            byte a = (byte)(v >> 24);
            byte r = (byte)(v >> 16);
            byte g = (byte)(v >> 8);
            byte b = (byte)(v & 0xFF);
            return new Color(r, g, b, a);
        }

        void MarkDirtyRect(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            x = Math.Max(0, Math.Min(x, width));
            y = Math.Max(0, Math.Min(y, height));
            w = Math.Max(0, Math.Min(w, width - x));
            h = Math.Max(0, Math.Min(h, height - y));

            if (!_dirty)
            {
                _dirty = true;
                _dirtyX = x;
                _dirtyY = y;
                _dirtyW = w;
                _dirtyH = h;
                return;
            }

            int x0 = Math.Min(_dirtyX, x);
            int y0 = Math.Min(_dirtyY, y);
            int x1 = Math.Max(_dirtyX + _dirtyW, x + w);
            int y1 = Math.Max(_dirtyY + _dirtyH, y + h);
            _dirtyX = x0;
            _dirtyY = y0;
            _dirtyW = x1 - x0;
            _dirtyH = y1 - y0;
        }

        public void ClearDirty()
        {
            _dirty = false;
            _dirtyX = _dirtyY = _dirtyW = _dirtyH = 0;
        }
    }
}
