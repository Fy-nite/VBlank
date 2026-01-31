using Microsoft.Xna.Framework;
using Adamantite.GFX;
using Microsoft.Xna.Framework.Input;

namespace Adamantite.GFX.UI
{
    public class Button : UIElement
    {
        public event System.Action? Clicked;
        public string Text { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressColor { get; set; }
        public bool IsPressed { get; private set; }

        public Button(Rectangle bounds, string text) : base(bounds)
        {
            Text = text;
            NormalColor = Colors.Gray;
            HoverColor = Colors.DarkGray;
            PressColor = Colors.White;
        }

        public override void Draw(Canvas canvas, Rectangle clip)
        {
            // basic button rendering
            var m = Mouse.GetState();
            bool over = m.X >= Bounds.X && m.X < Bounds.Right && m.Y >= Bounds.Y && m.Y < Bounds.Bottom;
            Color bg = over ? HoverColor : NormalColor;
            canvas.DrawFilledRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, bg);
            CanvasTextHelper.Prin(canvas, Bounds.X + 4, Bounds.Y + 2, Text, Colors.White);
        }

        internal void TriggerClick()
        {
            try { Clicked?.Invoke(); } catch { }
        }
    }
}
