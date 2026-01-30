using Microsoft.Xna.Framework.Content;
using ObjectIR.MonoGame.Helpers;

namespace AsmoV2.AudioEngine
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
