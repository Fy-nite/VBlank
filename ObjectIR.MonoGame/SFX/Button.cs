using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using ObjectIR.MonoGame.Helpers;

namespace ObjectIR.MonoGame.SFX
{
    public class Button
    {
        /// <summary>
        /// Play a short synthesized click sound through the provided SoundSystem.
        /// </summary>
        public static void PlayClick(SoundSystem soundSystem, string bus = "Master", float default_pitch = 440)
        {
            if (soundSystem == null) throw new ArgumentNullException(nameof(soundSystem));

            // synth click: render a short note from the MonoSynth and cache it
            const int sampleRate = 44100;
            const int channels = 1;
            float frequency = 1200f; // target pitch
            float duration = 0.06f; // how long the note is held before release

            // deterministic key describing the generated sound
            string key = $"button_click_synth_v1_{(int)frequency}_{duration}_{sampleRate}_{channels}";

            var effect = soundSystem.PrecomputeFromPcm(key, () =>
            {
                // create a synth, trigger note, render hold + release
                var synth = new SimpleSynth.MonoSynth(sampleRate, polyphony: 8);

                // convert frequency to approximate midi note
                double midiFloat = 69.0 + 12.0 * Math.Log(frequency / default_pitch, 2.0); // allow for changing the note pitch live
                int midiNote = Math.Clamp((int)Math.Round(midiFloat), 0, 127);

                synth.NoteOn(midiNote, 1f);

                int holdFrames = (int)Math.Ceiling(duration * sampleRate);
                // allow some frames for the release tail (approx 0.2s)
                int releaseFrames = (int)Math.Ceiling(0.2 * sampleRate);
                int totalFrames = holdFrames + releaseFrames;

                var pcm = synth.RenderPcm(holdFrames);
                // trigger release and render tail
                synth.NoteOff(midiNote);
                var tail = synth.RenderPcm(releaseFrames);

                // concatenate
                var outBytes = new byte[pcm.Length + tail.Length];
                Buffer.BlockCopy(pcm, 0, outBytes, 0, pcm.Length);
                Buffer.BlockCopy(tail, 0, outBytes, pcm.Length, tail.Length);
                return outBytes;
            }, sampleRate, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);

            soundSystem.PlayOneShot(effect, bus);
        }
    }
}
