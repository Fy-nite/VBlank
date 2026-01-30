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
        // Post-processing pipeline for generated PCM bytes. Each processor receives raw 16-bit PCM
        // little-endian bytes and returns new PCM bytes (must keep same sampleRate/channels or convert accordingly).
        public delegate byte[] PostProcessor(string key, byte[] pcm, int sampleRate, AudioChannels channels);
        private readonly List<PostProcessor> _postProcessors = new();

        /// <summary>
        /// Register a global post processor that will be available to apply when precomputing PCM.
        /// Processors are invoked in the order they were added when used explicitly.
        /// </summary>
        public void RegisterPostProcessor(PostProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            _postProcessors.Add(processor);
        }

        /// <summary>
        /// Unregister a previously registered post processor.
        /// </summary>
        public void UnregisterPostProcessor(PostProcessor processor)
        {
            if (processor == null) return;
            _postProcessors.Remove(processor);
        }
        private readonly ContentManager? _content;
        private readonly Dictionary<string, SoundBus> _buses = new();
        private readonly Dictionary<string, SoundEffect> _precomputed = new();
        private readonly object _precomputedLock = new();

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

        /// <summary>
        /// Get a precomputed SoundEffect by key, or create and cache it using the provided factory.
        /// Thread-safe.
        /// </summary>
        public SoundEffect GetOrCreatePrecomputed(string key, Func<SoundEffect> factory)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key");
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_precomputedLock)
            {
                if (_precomputed.TryGetValue(key, out var existing)) return existing;
                var created = factory();
                _precomputed[key] = created;
                return created;
            }
        }

        /// <summary>
        /// Create a SoundEffect from PCM bytes provided by pcmProvider and cache by key.
        /// </summary>
        public SoundEffect PrecomputeFromPcm(string key, Func<byte[]> pcmProvider, int sampleRate, AudioChannels channels = AudioChannels.Mono)
        {
            if (pcmProvider == null) throw new ArgumentNullException(nameof(pcmProvider));
            return GetOrCreatePrecomputed(key, () =>
            {
                var pcm = pcmProvider();
                // allow registered post-processors to modify the PCM
                foreach (var pp in _postProcessors)
                {
                    try
                    {
                        pcm = pp(key, pcm, sampleRate, channels);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("PostProcessor error: " + ex.Message);
                    }
                }
                return new SoundEffect(pcm, sampleRate, channels);
            });
        }

        public bool TryGetPrecomputed(string key, out SoundEffect? effect)
        {
            lock (_precomputedLock)
            {
                return _precomputed.TryGetValue(key, out effect);
            }
        }

        public void RemovePrecomputed(string key)
        {
            lock (_precomputedLock)
            {
                if (_precomputed.TryGetValue(key, out var e))
                {
                    _precomputed.Remove(key);
                    e.Dispose();
                }
            }
        }

        public void ClearPrecomputed()
        {
            lock (_precomputedLock)
            {
                foreach (var e in _precomputed.Values) e.Dispose();
                _precomputed.Clear();
            }
        }

    }
}
