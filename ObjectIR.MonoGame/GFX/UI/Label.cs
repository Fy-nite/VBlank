using Microsoft.Xna.Framework;
using Adamantite.GFX;

namespace Adamantite.GFX.UI
{
    public class Label : UIElement
    {
        public string Text { get; set; }
        public Color Color { get; set; }

        public Label(Rectangle bounds, string text, Color color) : base(bounds)
        {
            Text = text;
            Color = color;
        }

        public override void Draw(Canvas canvas, Rectangle clip)
        {
            // simple draw: background assumed handled elsewhere
            CanvasTextHelper.Prin(canvas, Bounds.X, Bounds.Y, Text, Color);
        }
    }
}
