using System;
using System.Collections.Generic;

namespace ObjectIR.MonoGame.SFX
{
    /// <summary>
    /// Simple mono polyphonic synthesizer with ADSR envelopes.
    /// Produces 16-bit PCM little-endian bytes suitable for MonoGame SoundEffect.
    /// </summary>
    public static class SimpleSynth
    {
        public enum Waveform
        {
            Sine,
            Square,
            Triangle,
            Noise
        }

        // Legacy helper kept for one-shot sine generation
        public static byte[] GenerateSineWavePcm(float frequencyHz, float durationSeconds, int sampleRate = 44100, float amplitude = 0.5f, int channels = 1)
        {
            if (durationSeconds <= 0) return Array.Empty<byte>();
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));

            int frames = (int)Math.Ceiling(durationSeconds * sampleRate);
            int samples = frames * channels;
            var bytes = new byte[samples * 2]; // 16-bit

            double twoPiF = 2.0 * Math.PI * frequencyHz;

            for (int i = 0; i < frames; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-10.0 * t);
                float sampleValue = (float)(amplitude * env * Math.Sin(twoPiF * t));
                short s = (short)Math.Clamp((int)(sampleValue * short.MaxValue), short.MinValue, short.MaxValue);

                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = (i * channels + ch) * 2;
                    bytes[idx] = (byte)(s & 0xFF);
                    bytes[idx + 1] = (byte)((s >> 8) & 0xFF);
                }
            }

            return bytes;
        }

        public static byte[] GenerateWavePcm(float frequencyHz, float durationSeconds, Waveform waveform, int sampleRate = 44100, float amplitude = 0.5f, int channels = 1)
        {
            if (durationSeconds <= 0) return Array.Empty<byte>();
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));

            int frames = (int)Math.Ceiling(durationSeconds * sampleRate);
            int samples = frames * channels;
            var bytes = new byte[samples * 2];

            double twoPiF = 2.0 * Math.PI * frequencyHz;
            var rng = Random.Shared;

            for (int i = 0; i < frames; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-10.0 * t);
                float osc = waveform switch
                {
                    Waveform.Square => MathF.Sign((float)Math.Sin(twoPiF * t)),
                    Waveform.Triangle => (float)(2.0 * Math.Abs(2.0 * (t * frequencyHz - Math.Floor(t * frequencyHz + 0.5))) - 1.0),
                    Waveform.Noise => (float)(rng.NextDouble() * 2.0 - 1.0),
                    _ => (float)Math.Sin(twoPiF * t)
                };
                float sampleValue = (float)(amplitude * env * osc);
                short s = (short)Math.Clamp((int)(sampleValue * short.MaxValue), short.MinValue, short.MaxValue);

                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = (i * channels + ch) * 2;
                    bytes[idx] = (byte)(s & 0xFF);
                    bytes[idx + 1] = (byte)((s >> 8) & 0xFF);
                }
            }

            return bytes;
        }

        // --- Synth core types ---

        private enum EnvState { Idle, Attack, Decay, Sustain, Release }

        private class Voice
        {
            public bool Active => state != EnvState.Idle;
            public int Note = -1; // midi
            public float Velocity = 1f;
            public double Phase = 0.0;
            public double PhaseInc = 0.0;
            private EnvState state = EnvState.Idle;
            private float envLevel = 0f;
            private double timeInState = 0.0;

            // ADSR params (seconds)
            public float Attack = 0.01f;
            public float Decay = 0.05f;
            public float Sustain = 0.7f; // level (0..1)
            public float Release = 0.1f;

            public void Start(int midiNote, float velocity, int sampleRate)
            {
                Note = midiNote;
                Velocity = Math.Clamp(velocity, 0f, 1f);
                Phase = 0.0;
                PhaseInc = MidiNoteToFreq(midiNote) * (2.0 * Math.PI) / sampleRate;
                state = EnvState.Attack;
                envLevel = 0f;
                timeInState = 0.0;
            }

            public void ReleaseNote()
            {
                if (state != EnvState.Idle && state != EnvState.Release)
                {
                    state = EnvState.Release;
                    timeInState = 0.0;
                }
            }

            public float ProcessSample()
            {
                if (state == EnvState.Idle) return 0f;

                // simple sine oscillator
                float sample = (float)(Math.Sin(Phase) * Velocity);
                Phase += PhaseInc;

                // envelope step: advance by one sample will be handled by caller via AdvanceEnvelope
                return sample * envLevel;
            }

            public void AdvanceEnvelope(double deltaSeconds)
            {
                timeInState += deltaSeconds;
                switch (state)
                {
                    case EnvState.Attack:
                        if (Attack <= 0)
                        {
                            envLevel = 1f;
                            state = EnvState.Decay;
                            timeInState = 0.0;
                        }
                        else
                        {
                            envLevel = (float)Math.Min(1.0, timeInState / Attack);
                            if (envLevel >= 1.0f)
                            {
                                state = EnvState.Decay;
                                timeInState = 0.0;
                            }
                        }
                        break;
                    case EnvState.Decay:
                        if (Decay <= 0)
                        {
                            envLevel = Sustain;
                            state = EnvState.Sustain;
                        }
                        else
                        {
                            float t = (float)Math.Min(1.0, timeInState / Decay);
                            envLevel = 1f + (Sustain - 1f) * t; // linear from 1 -> sustain
                            if (t >= 1.0f) state = EnvState.Sustain;
                        }
                        break;
                    case EnvState.Sustain:
                        envLevel = Sustain;
                        break;
                    case EnvState.Release:
                        if (Release <= 0)
                        {
                            envLevel = 0f;
                            state = EnvState.Idle;
                        }
                        else
                        {
                            float t = (float)Math.Min(1.0, timeInState / Release);
                            // linear fade to 0
                            envLevel = (1f - t) * envLevel;
                            if (t >= 1.0f || envLevel <= 1e-4f)
                            {
                                envLevel = 0f;
                                state = EnvState.Idle;
                            }
                        }
                        break;
                }
            }

            private static double MidiNoteToFreq(int note)
            {
                return 440.0 * Math.Pow(2.0, (note - 69) / 12.0);
            }
        }

        // end of Voice

        /// <summary>
        /// Public helper: convert MIDI note number (0-127) to frequency in Hz.
        /// </summary>
        public static double MidiNoteToFrequency(int note)
        {
            return 440.0 * Math.Pow(2.0, (note - 69) / 12.0);
        }

        /// <summary>
        /// Mono polyphonic synth instance.
        /// </summary>
        public sealed class MonoSynth
        {
            private readonly int sampleRate;
            private readonly List<Voice> voices = new();

            public float MasterVolume { get; set; } = 0.9f;

            public MonoSynth(int sampleRate = 44100, int polyphony = 8)
            {
                this.sampleRate = sampleRate > 0 ? sampleRate : 44100;
                for (int i = 0; i < Math.Max(1, polyphony); i++) voices.Add(new Voice());
            }

            /// <summary>
            /// Trigger a note on with midi note (0-127) and velocity (0..1).
            /// </summary>
            public void NoteOn(int midiNote, float velocity = 1f)
            {
                // find free voice
                Voice? v = null;
                foreach (var voice in voices)
                {
                    if (!voice.Active) { v = voice; break; }
                }
                // steal oldest if none free
                if (v == null) v = voices[0];

                v.Start(midiNote, velocity, sampleRate);
            }

            /// <summary>
            /// Release a note; if multiple voices with same midi note are active, release the first found.
            /// </summary>
            public void NoteOff(int midiNote)
            {
                foreach (var v in voices)
                {
                    if (v.Active && v.Note == midiNote)
                    {
                        v.ReleaseNote();
                        break;
                    }
                }
            }

            /// <summary>
            /// Render the next block of audio into 16-bit PCM little-endian mono bytes.
            /// </summary>
            public byte[] RenderPcm(int frames)
            {
                if (frames <= 0) return Array.Empty<byte>();
                var bytes = new byte[frames * 2];
                double dt = 1.0 / sampleRate;

                for (int i = 0; i < frames; i++)
                {
                    double mixed = 0.0;
                    foreach (var v in voices)
                    {
                        if (!v.Active) continue;
                        // advance envelope first for sample-accurate behavior
                        v.AdvanceEnvelope(dt);
                        mixed += v.ProcessSample();
                    }

                    // master volume and soft clipping
                    double s = Math.Clamp(mixed * MasterVolume, -1.0, 1.0);
                    short s16 = (short)(s * short.MaxValue);
                    int idx = i * 2;
                    bytes[idx] = (byte)(s16 & 0xFF);
                    bytes[idx + 1] = (byte)((s16 >> 8) & 0xFF);
                }

                return bytes;
            }
        }
    }
}
