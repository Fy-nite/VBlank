using System;
using Microsoft.Xna.Framework;
using Adamantite.GFX;

namespace Adamantite.GFX.UI
{
    // Simple ADSR graph UI element. Values provided by delegates so it can reflect live changes.
    public class AdsrGraph : UIElement
    {
        readonly Func<float> _getAttack;
        readonly Func<float> _getDecay;
        readonly Func<float> _getSustain;
        readonly Func<float> _getRelease;

        public AdsrGraph(Rectangle bounds, Func<float> getAttack, Func<float> getDecay, Func<float> getSustain, Func<float> getRelease) : base(bounds)
        {
            _getAttack = getAttack ?? throw new ArgumentNullException(nameof(getAttack));
            _getDecay = getDecay ?? throw new ArgumentNullException(nameof(getDecay));
            _getSustain = getSustain ?? throw new ArgumentNullException(nameof(getSustain));
            _getRelease = getRelease ?? throw new ArgumentNullException(nameof(getRelease));
        }

        public override void Draw(Canvas canvas, Rectangle clip)
        {
            // draw background
            canvas.DrawFilledRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, new Color(16, 18, 24, 255));
            canvas.DrawOutlinedRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, Colors.DarkGray);

            // read ADSR values
            float attack = _getAttack();
            float decay = _getDecay();
            float sustain = Math.Clamp(_getSustain(), 0f, 1f);
            float release = _getRelease();

            // normalize time slices (attack+decay+release), allocate some fixed fraction for sustain plateau
            float tTotal = Math.Max(0.0001f, attack + decay + release);
            int left = Bounds.X + 4;
            int right = Bounds.X + Bounds.Width - 4;
            int top = Bounds.Y + 6;
            int bottom = Bounds.Y + Bounds.Height - 6;
            float w = Math.Max(8, right - left);

            float attackFrac = attack / tTotal;
            float decayFrac = decay / tTotal;
            float releaseFrac = release / tTotal;

            float x0 = left;
            float x1 = left + attackFrac * w;
            float x2 = x1 + decayFrac * w;
            float x3 = x2 + 0.25f * w; // sustain plateau width (fixed small portion)
            float x4 = x3 + releaseFrac * w;
            if (x4 > right) x4 = right;

            float yBottom = bottom;
            float yPeak = top;
            float ySustain = yBottom - sustain * (yBottom - yPeak);

            // points
            var p0x = (int)x0; var p0y = (int)yBottom;
            var p1x = (int)x1; var p1y = (int)yPeak;
            var p2x = (int)x2; var p2y = (int)ySustain;
            var p3x = (int)x3; var p3y = (int)ySustain;
            var p4x = (int)x4; var p4y = (int)yBottom;

            // draw ADSR polyline
            canvas.DrawLine(p0x, p0y, p1x, p1y, Colors.Yellow);
            canvas.DrawLine(p1x, p1y, p2x, p2y, Colors.Yellow);
            canvas.DrawLine(p2x, p2y, p3x, p3y, Colors.Yellow);
            canvas.DrawLine(p3x, p3y, p4x, p4y, Colors.Yellow);

            // draw handles
            canvas.DrawFilledRect(p1x - 2, p1y - 2, 4, 4, Colors.Orange);
            canvas.DrawFilledRect(p2x - 2, p2y - 2, 4, 4, Colors.Orange);
            canvas.DrawFilledRect(p3x - 2, p3y - 2, 4, 4, Colors.Orange);

            // draw numeric labels
            canvas.DrawText(Bounds.X + 6, Bounds.Y + 2, $"A:{attack:0.000}", Colors.Gray);
            canvas.DrawText(Bounds.X + 6 + 56, Bounds.Y + 2, $"D:{decay:0.000}", Colors.Gray);
            canvas.DrawText(Bounds.X + Bounds.Width - 56, Bounds.Y + 2, $"S:{sustain:0.00}", Colors.Gray);
        }
    }
}
