using System;
using System.Collections.Generic;

namespace StarChart.stdlib.W11
{
    // Lightweight W11 profiler window implemented with XTerm text rendering.
    public class ProfilerWindow
    {
        readonly XTerm _term;
        double _accum;

        public Window Window => _term.Window;

        public ProfilerWindow(DisplayServer server, int x, int y, int scale = 1)
        {
            _term = new XTerm(server, "profiler", "Profiler", 30, 30, x, y, scale, XTerm.FontKind.Small4x6);
            _term.Window.Map();
        }

        public void Update(double deltaTime, int windows, int dirtyRects, bool fullRedraw, double drawMs, double updateMs)
        {
            _accum += deltaTime;
            if (_accum < 0.25) return;
            _accum = 0;

            double fps = deltaTime > 0 ? 1.0 / deltaTime : 0;
            var lines = new List<string>
            {
                $"FPS: {fps:0.0}",
                $"dt: {deltaTime * 1000:0.0} ms",
                $"draw: {drawMs:0.0} ms",
                $"update: {updateMs:0.0} ms",
                $"wins: {windows}",
                $"dirty: {dirtyRects}",
                $"full: {fullRedraw}",
            };
            _term.SetLines(lines);
        }
    }
}
