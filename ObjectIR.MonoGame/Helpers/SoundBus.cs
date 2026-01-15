using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIR.MonoGame.Helpers
{
    public class SoundBus
    {
        public string Name { get; set; }
        /// <summary>
        /// Volume multiplier for this bus (0.0 - 1.0). Default is 1.0 (no change).
        /// </summary>
        public float Volume { get; set; } = 1f;

        public SoundBus() { }
        public SoundBus(string name) { Name = name; }


    }
}
