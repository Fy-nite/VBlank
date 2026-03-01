using Microsoft.Xna.Framework.Content;
using ObjectIR.MonoGame.Helpers;

namespace VBlank.AudioEngine
{
    /// <summary>
    /// Simple host for the ObjectIR audio helpers used by the main game.
    /// Exposes a single shared SoundSystem instance that can be initialized
    /// with the game's ContentManager.
    /// </summary>
    public static class Subsystem
    {
        public static SoundSystem? Sound { get; private set; }

        public static void Initialize(ContentManager content)
        {
            if (Sound == null)
            {
                Sound = new SoundSystem(content);
            }
        }

        /// <summary>
        /// Initialize the sound subsystem without a ContentManager.
        /// This uses the parameterless SoundSystem ctor which supports
        /// creating SoundEffect instances from streams.
        /// </summary>
        public static void Initialize()
        {
            if (Sound == null)
            {
                Sound = new SoundSystem();
            }
        }

        public static void Shutdown()
        {
            if (Sound != null)
            {
                Sound.ClearPrecomputed();
                Sound = null;
            }
        }
    }
}
