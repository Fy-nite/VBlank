using System;

namespace Adamantite.GFX
{
    /// <summary>
    /// Lightweight FPS counter helper. Call Tick with the elapsed seconds for the current frame
    /// (use GameTime.ElapsedGameTime.TotalSeconds). The <see cref="Fps"/> value is updated
    /// about once per second.
    /// </summary>
    public sealed class FpsCounter
    {
        private double _accumSeconds;
        private int _frames;
        private double _fps;

        /// <summary>
        /// Current measured frames per second (smoothed over the last measurement window).
        /// </summary>
        public double Fps => _fps;

        /// <summary>
        /// Tick the counter with the elapsed seconds for this frame.
        /// </summary>
        public void Tick(double elapsedSeconds)
        {
            if (elapsedSeconds < 0) elapsedSeconds = 0;
            _accumSeconds += elapsedSeconds;
            _frames++;

            if (_accumSeconds >= 1.0)
            {
                _fps = _frames / _accumSeconds;
                _frames = 0;
                _accumSeconds = 0.0;
            }
        }

        /// <summary>
        /// Reset internal counters.
        /// </summary>
        public void Reset()
        {
            _accumSeconds = 0.0;
            _frames = 0;
            _fps = 0.0;
        }
    }
}
