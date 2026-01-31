using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Adamantite.GFX;

namespace Adamantite.GFX.UI
{
    public class UIManager
    {
        readonly List<UIElement> _elements = new();
        // simple dirty rect list accumulated per frame
        readonly List<Rectangle> _dirty = new();
        Microsoft.Xna.Framework.Input.MouseState _prevMouse;

        public void Add(UIElement e)
        {
            if (e == null) return;
            if (_elements.Contains(e)) return;
            e.Manager = this;
            _elements.Add(e);
            // mark element area dirty so it draws first frame
            NotifyInvalid(e.Bounds);
        }

        // Basic input handling to detect clicks on buttons. Should be called every frame by the host.
        // `scale` converts window pixels into canvas pixels (canvas = window / scale)
        public void ProcessInput(float scale)
        {
            var m = Microsoft.Xna.Framework.Input.Mouse.GetState();
            if (_prevMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && m.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
            {
                // Map window-space mouse coords into canvas-space using scale
                int mx = (int)(m.X / Math.Max(0.0001f, scale));
                int my = (int)(m.Y / Math.Max(0.0001f, scale));
                foreach (var e in _elements)
                {
                    if (e is Button btn)
                    {
                        if (mx >= btn.Bounds.X && mx < btn.Bounds.Right && my >= btn.Bounds.Y && my < btn.Bounds.Bottom)
                        {
                            btn.TriggerClick();
                            btn.Invalidate();
                        }
                    }
                }
            }
            _prevMouse = m;
        }

        public void Remove(UIElement e)
        {
            if (e == null) return;
            _elements.Remove(e);
            e.Manager = null;
        }

        // Called by UIElement.Invalidate and by hosts to mark regions dirty
        public void NotifyInvalid(Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            _dirty.Add(r);
        }

        // Merge dirty rects into minimal set (very simple merging for now)
        List<Rectangle> CoalesceDirty()
        {
            if (_dirty.Count == 0) return new List<Rectangle>();
            var list = new List<Rectangle>(_dirty);
            // naive merging: if rects intersect or touch, union them
            bool mergedAny;
            do
            {
                mergedAny = false;
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var a = list[i];
                        var b = list[j];
                        var expanded = Rectangle.Union(a, b);
                        // if they intersect or are adjacent (touching), merge
                        if (expanded.Width <= a.Width + b.Width + 4 && expanded.Height <= a.Height + b.Height + 4)
                        {
                            list[i] = expanded;
                            list.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                    }
                    if (mergedAny) break;
                }
            } while (mergedAny);

            return list;
        }

        // Draw only elements intersecting dirty regions and return the final dirty regions
        public IReadOnlyList<Rectangle> RenderDirty(Canvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            var coalesced = CoalesceDirty();
            if (coalesced.Count == 0) return coalesced;

            // For each dirty region, clear it (or leave background handling to elements)
            foreach (var r in coalesced)
            {
                // clip to canvas
                var clip = Rectangle.Intersect(new Rectangle(0,0,canvas.width, canvas.height), r);
                if (clip.Width <= 0 || clip.Height <= 0) continue;
                // Draw all elements that intersect the region
                foreach (var e in _elements)
                {
                    if (!e.Visible) continue;
                    if (!e.Bounds.Intersects(clip)) continue;
                    e.Draw(canvas, clip);
                }
            }

            var result = coalesced;
            _dirty.Clear();
            return result;
        }
    }
}
