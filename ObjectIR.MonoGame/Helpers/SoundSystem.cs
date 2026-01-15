using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace ObjectIR.MonoGame.Helpers
{
    public class SoundSystem
    {
        private readonly ContentManager? _content;
        private readonly Dictionary<string, SoundBus> _buses = new();

        public SoundBus Master { get; } = new SoundBus("Master") { Volume = 1f };

        public SoundSystem()
        {
            _buses[Master.Name] = Master;
        }

        public SoundSystem(ContentManager content) : this()
        {
            _content = content;
        }

        /// <summary>
        /// Create or update a named bus with a given volume (0..1).
        /// </summary>
        public void CreateBus(string name, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");
            if (_buses.TryGetValue(name, out var b))
            {
                b.Volume = Math.Clamp(volume, 0f, 1f);
            }
            else
            {
                _buses[name] = new SoundBus(name) { Volume = Math.Clamp(volume, 0f, 1f) };
            }
        }

        public bool TryGetBus(string name, out SoundBus? bus) => _buses.TryGetValue(name, out bus);

        public void SetBusVolume(string name, float volume)
        {
            if (_buses.TryGetValue(name, out var b)) b.Volume = Math.Clamp(volume, 0f, 1f);
            else CreateBus(name, volume);
        }

        /// <summary>
        /// Load a SoundEffect using the ContentManager provided in the ctor.
        /// Throws if no ContentManager was provided.
        /// </summary>
        public SoundEffect Load(string assetName)
        {
            if (_content == null) throw new InvalidOperationException("ContentManager was not provided to SoundSystem.");
            return _content.Load<SoundEffect>(assetName);
        }

        /// <summary>
        /// Create a SoundEffect from a raw stream (wav).
        /// </summary>
        public SoundEffect FromStream(Stream stream)
        {
            return SoundEffect.FromStream(stream);
        }

        /// <summary>
        /// Play a SoundEffect asset by name and return the instance for further control.
        /// </summary>
        public SoundEffectInstance Play(string assetName, string busName = "Master", float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            var se = Load(assetName);
            return Play(se, busName, volume, pitch, pan);
        }

        /// <summary>
        /// Play a provided SoundEffect and return the instance.
        /// </summary>
        public SoundEffectInstance Play(SoundEffect effect, string busName = "Master", float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            var instance = effect.CreateInstance();
            var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
            instance.Volume = Math.Clamp(volume * busVol, 0f, 1f);
            instance.Pitch = Math.Clamp(pitch, -1f, 1f);
            instance.Pan = Math.Clamp(pan, -1f, 1f);
            instance.Play();
            return instance;
        }

        /// <summary>
        /// Play a one-shot using SoundEffect.Play with bus volume applied.
        /// </summary>
        public void PlayOneShot(string assetName, string busName = "Master", float volume = 1f)
        {
            var se = Load(assetName);
            PlayOneShot(se, busName, volume);
        }

        public void PlayOneShot(SoundEffect effect, string busName = "Master", float volume = 1f)
        {
            var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
            // Provide default values for pitch and pan as required by SoundEffect.Play(float, float, float)
            effect.Play(Math.Clamp(volume * busVol, 0f, 1f), 0f, 0f);
        }

    }
}
