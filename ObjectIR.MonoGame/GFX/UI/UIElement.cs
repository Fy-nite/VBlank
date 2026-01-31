using System;
using Microsoft.Xna.Framework;

namespace Adamantite.GFX.UI
{
    public abstract class UIElement
    {
        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; } = true;

        internal UIManager? Manager { get; set; }

        protected UIElement(Rectangle bounds)
        {
            Bounds = bounds;
        }

        // Called by UIManager when the element should draw itself into the canvas
        public abstract void Draw(Adamantite.GFX.Canvas canvas, Rectangle clip);

        // Request the manager to invalidate this element (or a sub-rect)
        public void Invalidate(Rectangle? area = null)
        {
            if (Manager == null) return;
            var r = area ?? Bounds;
            // intersect with element bounds
            r = Rectangle.Intersect(r, Bounds);
            if (r.Width <= 0 || r.Height <= 0) return;
            Manager.NotifyInvalid(r);
        }
    }
}
