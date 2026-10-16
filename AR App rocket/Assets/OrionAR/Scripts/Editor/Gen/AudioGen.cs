using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Synthesises every sound in the app and writes them as WAV assets.
    ///
    /// Generated rather than sourced, for the same reason the textures are: the
    /// project ships with nothing whose licence has to be declared or defended. It
    /// also means the whole palette is tuned together - the UI sounds share a tonal
    /// centre, so they feel like one instrument rather than a pile of downloads.
    ///
    /// The palette is built on a C minor pentatonic scale. Pentatonic intervals avoid
    /// the harsh semitone clashes that make UI sounds grating when heard hundreds of
    /// times, which at an exhibition they will be.
    /// </summary>
    public static class AudioGen
    {
        public const string Folder = "Assets/OrionAR/Audio";
        const int SampleRate = 44100;

        // C minor pentatonic, two octaves. Everything tonal is drawn from here.
        static readonly float[] Scale = {
            261.63f, 311.13f, 349.23f, 392.00f, 466.16f,
            523.25f, 622.25f, 698.46f, 784.00f, 932.33f, 1046.50f
        };

        public static void GenerateAll()
        {
            AssetPaths.EnsureFolder(Folder);

            // --- UI ---------------------------------------------------
            Write("ui_tap", Tap());
            Write("ui_open", Swell(up: true));
            Write("ui_close", Swell(up: false));
            Write("ui_check", Check());
            Write("ui_place", Place());
            Write("ui_launch", Launch());

            // --- Quiz -------------------------------------------------
            Write("quiz_next", Tap(pitchIndex: 3, gain: 0.35f));
            Write("quiz_correct", Arpeggio(new[] { 5, 7, 9 }, 0.09f, 0.42f));
            Write("quiz_wrong", Wrong());
            Write("quiz_pass", Fanfare());
            Write("quiz_fail", Arpeggio(new[] { 4, 2, 0 }, 0.14f, 0.33f));

            // --- Countdown and engine ---------------------------------
            Write("beep_low", Beep(640f, 0.10f, 0.26f));
            Write("beep_high", Beep(980f, 0.13f, 0.34f));
            Write("engine_ignite", Ignition());
            Write("engine_loop", EngineLoop());
            Write("engine_cutoff", Cutoff());

            // --- Milestones -------------------------------------------
            Write("tour_complete", Arpeggio(new[] { 5, 7, 9, 10 }, 0.11f, 0.42f));
            Write("mission_success", Fanfare(longForm: true));

            // --- Ambience ---------------------------------------------
            Write("amb_menu", Ambience(60f, 0.055f, 2.2f));
            Write("amb_orbit", Ambience(48f, 0.045f, 3.0f));
            Write("amb_earth", Ambience(72f, 0.05f, 2.0f));

            AssetDatabase.Refresh();
            Debug.Log("[AudioGen] Generated 19 audio clips into " + Folder);
        }

        // ================================================================
        // UI sounds
        // ================================================================

        /// <summary>Short filtered click. Deliberately quiet: it fires constantly.</summary>
        static float[] Tap(int pitchIndex = 5, float gain = 0.30f)
        {
            float dur = 0.055f;
            var buf = new float[(int)(SampleRate * dur)];
            float f = Scale[pitchIndex];

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f);
                float body = Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f;
                // A touch of noise gives the transient its "click" rather than "beep".
                float click = (Noise(i) * 0.4f) * Mathf.Exp(-t * 400f);
                buf[i] = (body + click) * env * gain;
            }
            return buf;
        }

        /// <summary>Rising or falling sweep for sheets opening and closing.</summary>
        static float[] Swell(bool up)
        {
            float dur = 0.22f;
            var buf = new float[(int)(SampleRate * dur)];

            float f0 = up ? Scale[2] : Scale[7];
            float f1 = up ? Scale[7] : Scale[2];

            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float k = t / dur;
                float f = Mathf.Lerp(f0, f1, UITheme.EaseOut(k));

                phase += 2f * Mathf.PI * f / SampleRate;

                float env = Mathf.Sin(k * Mathf.PI);      // fade in and out
                buf[i] = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.18f) * env * 0.22f;
            }
            return buf;
        }

        /// <summary>Two-note confirmation when a component is logged as studied.</summary>
        static float[] Check()
        {
            return Arpeggio(new[] { 5, 8 }, 0.075f, 0.34f);
        }

        /// <summary>Solid thunk when the model is anchored to the world.</summary>
        static float[] Place()
        {
            float dur = 0.45f;
            var buf = new float[(int)(SampleRate * dur)];

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;

                // Low body that drops in pitch: reads as weight landing.
                float f = Mathf.Lerp(150f, 72f, Mathf.Clamp01(t * 14f));
                float body = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 11f);

                float thud = Noise(i) * Mathf.Exp(-t * 55f) * 0.3f;
                float ring = Mathf.Sin(2f * Mathf.PI * Scale[5] * t) * Mathf.Exp(-t * 7f) * 0.18f;

                buf[i] = (body * 0.7f + thud + ring) * 0.5f;
            }
            return buf;
        }

        static float[] Launch()
        {
            float dur = 0.8f;
            var buf = new float[(int)(SampleRate * dur)];
            float phase = 0f;

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float k = t / dur;

                float f = Mathf.Lerp(110f, 520f, UITheme.EaseInOut(k));
                phase += 2f * Mathf.PI * f / SampleRate;

                float env = Mathf.Min(1f, k * 6f) * Mathf.Exp(-Mathf.Max(0f, k - 0.55f) * 7f);
                float rumble = LowNoise(i, 0.22f) * 0.35f;

                buf[i] = (Mathf.Sin(phase) * 0.5f + rumble) * env * 0.45f;
            }
            return buf;
        }

        // ================================================================
        // Quiz
        // ================================================================

        /// <summary>
        /// Descending minor second. Unambiguously "no" without being punishing -
        /// a harsh buzzer discourages people from trying again.
        /// </summary>
        static float[] Wrong()
        {
            float dur = 0.34f;
            var buf = new float[(int)(SampleRate * dur)];

            float fA = 311.13f, fB = 293.66f;

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 60f);

                float a = Mathf.Sin(2f * Mathf.PI * fA * t);
                float b = Mathf.Sin(2f * Mathf.PI * fB * t);

                buf[i] = (a * 0.5f + b * 0.5f) * env * 0.26f;
            }
            return buf;
        }

        static float[] Arpeggio(int[] indices, float noteGap, float gain)
        {
            float noteDur = 0.30f;
            float total = noteGap * (indices.Length - 1) + noteDur;
            var buf = new float[(int)(SampleRate * total)];

            for (int n = 0; n < indices.Length; n++)
            {
                float f = Scale[Mathf.Clamp(indices[n], 0, Scale.Length - 1)];
                int start = (int)(n * noteGap * SampleRate);

                for (int i = 0; i < (int)(noteDur * SampleRate); i++)
                {
                    int idx = start + i;
                    if (idx >= buf.Length) break;

                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 8f) * Mathf.Min(1f, t * 150f);

                    float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.62f
                            + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.22f
                            + Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.08f;

                    buf[idx] += s * env * gain;
                }
            }
            return Normalise(buf, 0.75f);
        }

        static float[] Fanfare(bool longForm = false)
        {
            // Root, fifth, octave, then a sustained triad underneath.
            int[] lead = longForm ? new[] { 0, 3, 5, 8, 10 } : new[] { 0, 3, 5, 8 };
            float noteGap = 0.13f;
            float total = noteGap * lead.Length + (longForm ? 2.2f : 1.4f);
            var buf = new float[(int)(SampleRate * total)];

            for (int n = 0; n < lead.Length; n++)
            {
                float f = Scale[lead[n]];
                int start = (int)(n * noteGap * SampleRate);
                int len = (int)(0.9f * SampleRate);

                for (int i = 0; i < len; i++)
                {
                    int idx = start + i;
                    if (idx >= buf.Length) break;
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 3.2f) * Mathf.Min(1f, t * 120f);
                    buf[idx] += (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f +
                                 Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f) * env * 0.4f;
                }
            }

            // Sustained pad: root, minor third, fifth.
            float[] pad = { Scale[0] * 0.5f, Scale[1] * 0.5f, Scale[3] * 0.5f };
            int padStart = (int)(noteGap * SampleRate);
            for (int i = 0; i + padStart < buf.Length; i++)
            {
                int idx = i + padStart;
                float t = (float)i / SampleRate;
                float env = Mathf.Min(1f, t * 2.2f) * Mathf.Exp(-Mathf.Max(0f, t - 0.8f) * 1.6f);

                float s = 0f;
                foreach (var f in pad) s += Mathf.Sin(2f * Mathf.PI * f * t);
                buf[idx] += (s / pad.Length) * env * 0.22f;
            }

            return Normalise(buf, 0.8f);
        }

        static float[] Beep(float freq, float dur, float gain)
        {
            var buf = new float[(int)(SampleRate * dur)];
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-Mathf.Max(0f, t - dur * 0.55f) * 36f);
                buf[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * gain;
            }
            return buf;
        }

        // ================================================================
        // Engine
        // ================================================================

        /// <summary>Ignition: a sharp transient opening into broadband roar.</summary>
        static float[] Ignition()
        {
            float dur = 2.2f;
            var buf = new float[(int)(SampleRate * dur)];

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;

                float crack = Noise(i) * Mathf.Exp(-t * 26f) * 0.55f;

                float build = Mathf.Min(1f, t * 1.5f);
                float roar = LowNoise(i, 0.14f) * build * 0.6f;

                float sub = Mathf.Sin(2f * Mathf.PI * 38f * t) * build * 0.3f;

                buf[i] = (crack + roar + sub) * 0.65f;
            }
            return Normalise(buf, 0.85f);
        }

        /// <summary>
        /// Seamless engine loop.
        ///
        /// Looping noise is the awkward case: a raw random buffer clicks at the seam.
        /// This builds the rumble from sine partials whose frequencies are exact
        /// multiples of the loop frequency, so the waveform is periodic by
        /// construction and the loop point is inaudible.
        /// </summary>
        static float[] EngineLoop()
        {
            float dur = 2.0f;
            int length = (int)(SampleRate * dur);
            var buf = new float[length];

            float loopFreq = 1f / dur;                 // fundamental that fits exactly
            var rng = new System.Random(4071);

            // 90 partials across the rumble band, each an integer multiple of the
            // loop frequency so everything closes perfectly at the seam.
            for (int p = 0; p < 90; p++)
            {
                int harmonic = 24 + p * 7 + rng.Next(0, 6);
                float f = harmonic * loopFreq;
                if (f > 1200f) continue;

                float amp = 1f / (1f + Mathf.Pow(f / 90f, 1.35f));
                float phase = (float)rng.NextDouble() * Mathf.PI * 2f;

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * f * t + phase) * amp;
                }
            }

            // Slow amplitude wander, also periodic over the loop.
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float wander = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * loopFreq * t)
                                  + 0.07f * Mathf.Sin(2f * Mathf.PI * loopFreq * 3f * t);
                buf[i] *= wander;
            }

            return Normalise(buf, 0.62f);
        }

        static float[] Cutoff()
        {
            float dur = 1.3f;
            var buf = new float[(int)(SampleRate * dur)];

            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float decay = Mathf.Exp(-t * 4.2f);
                float rumble = LowNoise(i, 0.12f) * decay * 0.5f;
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(52f, 24f, Mathf.Clamp01(t * 1.6f)) * t)
                            * decay * 0.35f;
                buf[i] = (rumble + sub) * 0.7f;
            }
            return Normalise(buf, 0.8f);
        }

        // ================================================================
        // Ambience
        // ================================================================

        /// <summary>
        /// Low drone bed. Same periodic-partial trick as the engine loop so it can
        /// run continuously without a seam.
        /// </summary>
        static float[] Ambience(float baseFreq, float gain, float dur)
        {
            int length = (int)(SampleRate * dur);
            var buf = new float[length];
            float loopFreq = 1f / dur;

            // Snap the drone to an exact multiple of the loop frequency.
            int rootHarmonic = Mathf.Max(1, Mathf.RoundToInt(baseFreq / loopFreq));
            var rng = new System.Random(911);

            float[] ratios = { 1f, 1.5f, 2f, 3f, 4.02f, 6f };
            foreach (var ratio in ratios)
            {
                int harmonic = Mathf.RoundToInt(rootHarmonic * ratio);
                float f = harmonic * loopFreq;
                float amp = 1f / (1f + ratio * 1.6f);
                float phase = (float)rng.NextDouble() * Mathf.PI * 2f;

                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / SampleRate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * f * t + phase) * amp;
                }
            }

            // Gentle breathing, one cycle per loop.
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                buf[i] *= 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * loopFreq * t);
            }

            return Normalise(buf, gain * 6f);
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>Deterministic white noise, so builds are reproducible.</summary>
        static float Noise(int i)
        {
            unchecked
            {
                int n = i * 1619 + 31337;
                n = (n << 13) ^ n;
                int m = n * (n * n * 15731 + 789221) + 1376312589;
                return ((m & 0x7fffffff) / 1073741823f) - 1f;
            }
        }

        /// <summary>Noise through a one-pole low-pass, giving rumble rather than hiss.</summary>
        static float _lowState;
        static float LowNoise(int i, float cutoff)
        {
            if (i == 0) _lowState = 0f;
            _lowState += (Noise(i) - _lowState) * cutoff;
            return _lowState;
        }

        static float[] Normalise(float[] buf, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            float scale = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
            return buf;
        }

        /// <summary>Writes 16-bit mono PCM and configures the import settings.</summary>
        static void Write(string name, float[] samples)
        {
            string path = Folder + "/" + name + ".wav";

            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;

                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });

                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);                       // chunk size
                w.Write((short)1);                 // PCM
                w.Write((short)1);                 // mono
                w.Write(SampleRate);
                w.Write(SampleRate * 2);           // byte rate
                w.Write((short)2);                 // block align
                w.Write((short)16);                // bits per sample

                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);

                foreach (var s in samples)
                    w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));

                File.WriteAllBytes(path, stream.ToArray());
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp != null)
            {
                bool isLoop = name.StartsWith("amb_") || name == "engine_loop";
                var settings = imp.defaultSampleSettings;

                // Short UI sounds decompress into memory so they fire with no latency;
                // longer beds stream in compressed form to keep memory down.
                settings.loadType = isLoop
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isLoop
                    ? AudioCompressionFormat.Vorbis
                    : AudioCompressionFormat.PCM;
                settings.quality = 0.7f;

                settings.preloadAudioData = true;
                imp.defaultSampleSettings = settings;
                imp.forceToMono = true;
                imp.loadInBackground = false;
                imp.SaveAndReimport();
            }
        }

        public static AudioClip Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + ".wav");
        }

        public static List<string> AllKeys()
        {
            return new List<string>
            {
                "ui_tap", "ui_open", "ui_close", "ui_check", "ui_place", "ui_launch",
                "quiz_next", "quiz_correct", "quiz_wrong", "quiz_pass", "quiz_fail",
                "beep_low", "beep_high",
                "engine_ignite", "engine_loop", "engine_cutoff",
                "tour_complete", "mission_success",
                "amb_menu", "amb_orbit", "amb_earth",
            };
        }
    }
}
