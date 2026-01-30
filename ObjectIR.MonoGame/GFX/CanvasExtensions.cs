using System;
using Microsoft.Xna.Framework;

namespace Adamantite.GFX
{
    public static class CanvasExtensions
    {
        public static void DrawRect(this Canvas c, int x, int y, int w, int h, Color color)
        {
            c.DrawFilledRect(x, y, w, h, color);
        }

        public static void DrawOutlinedRect(this Canvas c, int x, int y, int w, int h, Color color)
        {
            if (w <= 0 || h <= 0) return;
            c.DrawLine(x, y, x + w - 1, y, color);
            c.DrawLine(x, y + h - 1, x + w - 1, y + h - 1, color);
            c.DrawLine(x, y, x, y + h - 1, color);
            c.DrawLine(x + w - 1, y, x + w - 1, y + h - 1, color);
        }

        public static void DrawLine(this Canvas c, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                c.SetPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        public static void DrawText(this Canvas c, int x, int y, string text, Color color)
        {
            CanvasTextHelper.Prin(c, x, y, text, color);
        }
    }
}
