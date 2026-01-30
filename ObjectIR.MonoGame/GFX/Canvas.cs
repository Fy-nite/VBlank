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

        public Canvas(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.x = 0;
            this.y = 0;
            PixelData = new Color[width * height];
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < PixelData.Length; i++) PixelData[i] = c;
        }

        public void SetPixel(int x, int y, Color c)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            PixelData[y * width + x] = c;
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
    }
}
