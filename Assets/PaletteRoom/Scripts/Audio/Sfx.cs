using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    public enum SfxKind
    {
        /// <summary>A button or card pressed.</summary>
        Click,
        /// <summary>The pointer moves onto something pressable (very quiet).</summary>
        Hover,
        /// <summary>A panel opens.</summary>
        Open,
        /// <summary>A panel closes.</summary>
        Close,
        /// <summary>A piece or panel is picked up.</summary>
        PickUp,
        /// <summary>A piece is set down.</summary>
        Drop,
        /// <summary>A new piece pops into the room.</summary>
        Pop,
        /// <summary>Something worked: a tutorial step done, a palette saved.</summary>
        Success,
        /// <summary>Something can't be done here.</summary>
        Error,
        /// <summary>The intro's sweep.</summary>
        Whoosh,
    }

    /// <summary>
    /// Interface and interaction sounds, all made in code (no audio files): soft wooden clicks, a quiet
    /// hover tick, rising and falling panel tones, a lift and a thud for furniture, a bubbly pop for new
    /// pieces, a success arpeggio and a gentle "no". Played 2D for the interface and 3D from furniture.
    /// Volume follows the Effects slider.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        const int Voices2D = 6, Voices3D = 6;
        const float HoverGap = 0.06f;

        static Dictionary<SfxKind, AudioClip> s_Clips;
        static readonly List<AudioSource> s_2D = new List<AudioSource>();
        static readonly List<AudioSource> s_3D = new List<AudioSource>();
        static Transform s_Root;
        static float s_LastHover;
        static int s_Next2D, s_Next3D;

        /// <summary>Plays a sound for the interface, not placed in the room.</summary>
        public static void Play(SfxKind kind, float volume = 1f, float pitch = 1f)
        {
            if (!Ready() || !RateOk(kind))
                return;
            var source = s_2D[s_Next2D = (s_Next2D + 1) % s_2D.Count];
            source.pitch = pitch;
            source.PlayOneShot(s_Clips[kind], volume * Loudness(kind) * AudioLevels.EffectsGain);
        }

        /// <summary>Plays a sound from a place in the room.</summary>
        public static void PlayAt(SfxKind kind, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!Ready() || !RateOk(kind))
                return;
            var source = s_3D[s_Next3D = (s_Next3D + 1) % s_3D.Count];
            source.transform.position = position;
            source.pitch = pitch;
            source.PlayOneShot(s_Clips[kind], volume * Loudness(kind) * AudioLevels.EffectsGain);
        }

        static bool RateOk(SfxKind kind)
        {
            if (kind != SfxKind.Hover)
                return true;
            if (Time.unscaledTime - s_LastHover < HoverGap)
                return false;
            s_LastHover = Time.unscaledTime;
            return true;
        }

        static float Loudness(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.Hover: return 0.18f;
                case SfxKind.Click: return 0.55f;
                case SfxKind.Open:
                case SfxKind.Close: return 0.35f;
                case SfxKind.Whoosh: return 0.5f;
                default: return 0.7f;
            }
        }

        static bool Ready()
        {
            if (!Application.isPlaying)
                return false;
            if (s_Root != null)
                return true;
            AudioLevels.Ensure();
            s_Clips ??= MakeClips();
            var root = new GameObject("Sound Effects");
            UnityEngine.Object.DontDestroyOnLoad(root);
            s_Root = root.transform;
            s_2D.Clear();
            s_3D.Clear();
            for (int i = 0; i < Voices2D; i++)
                s_2D.Add(NewSource($"UI Voice {i + 1}", spatial: false));
            for (int i = 0; i < Voices3D; i++)
                s_3D.Add(NewSource($"Room Voice {i + 1}", spatial: true));
            return true;
        }

        static AudioSource NewSource(string name, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(s_Root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                source.spatialize = true; // the platform spatializer when one is set
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 0.6f;
                source.maxDistance = 14f;
            }
            return source;
        }

        // ---- the sounds ------------------------------------------------------------------------

        static Dictionary<SfxKind, AudioClip> MakeClips()
        {
            var rng = new System.Random(11);
            return new Dictionary<SfxKind, AudioClip>
            {
                [SfxKind.Click] = Make("Click", 0.07f, (t, i) =>
                    Tone(t, 1250f - 500f * t / 0.07f, 70f) * 0.8f + Noise(rng) * Env(t, 0.001f, 220f) * 0.25f),
                [SfxKind.Hover] = Make("Hover", 0.03f, (t, i) => Tone(t, 2600f, 140f)),
                [SfxKind.Open] = Make("Open", 0.32f, (t, i) =>
                    Bell(t, 659.3f) * 0.6f + Bell(t - 0.07f, 987.8f) * 0.55f),
                [SfxKind.Close] = Make("Close", 0.3f, (t, i) =>
                    Bell(t, 880f) * 0.55f + Bell(t - 0.07f, 587.3f) * 0.6f),
                [SfxKind.PickUp] = Make("Pick Up", 0.22f, (t, i) =>
                    Sweep(t, 320f, 720f, 0.22f) * Env(t, 0.02f, 12f) * 0.5f + Noise(rng) * Env(t, 0.03f, 18f) * 0.12f),
                [SfxKind.Drop] = Make("Drop", 0.3f, (t, i) =>
                    Sweep(t, 140f, 55f, 0.18f) * Env(t, 0.002f, 16f) + Tone(t, 900f, 90f) * 0.25f + Noise(rng) * Env(t, 0.001f, 60f) * 0.2f),
                [SfxKind.Pop] = Make("Pop", 0.16f, (t, i) =>
                    Sweep(t, 280f, 1100f, 0.07f) * Env(t, 0.002f, 26f) + Bell(t - 0.03f, 1318.5f) * 0.25f),
                [SfxKind.Success] = Make("Success", 0.75f, (t, i) =>
                    Bell(t, 1046.5f) * 0.5f + Bell(t - 0.09f, 1318.5f) * 0.45f + Bell(t - 0.18f, 1568f) * 0.5f),
                [SfxKind.Error] = Make("Error", 0.32f, (t, i) =>
                    Buzz(t, 196f) * Env(t, 0.005f, 14f) * 0.5f + Buzz(t - 0.13f, 175f) * Env(t - 0.13f, 0.005f, 14f) * 0.5f),
                [SfxKind.Whoosh] = Make("Whoosh", 0.9f, (t, i) =>
                    Noise(rng) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f)) * 0.35f + Sweep(t, 200f, 600f, 0.9f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f)) * 0.15f),
            };
        }

        static AudioClip Make(string name, float seconds, Func<float, int, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample(i / (float)Rate, i);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Even loudness between sounds; Loudness() then sets each one's level.
            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++)
            {
                float fadeOut = Mathf.Clamp01((n - i) / (Rate * 0.005f)); // no click at the end
                data[i] *= gain * fadeOut;
            }
            var clip = AudioClip.Create("Sfx " + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float attack, float decay) => t < 0f ? 0f : Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * t);

        static float Tone(float t, float hz, float decay) => Mathf.Sin(2f * Mathf.PI * hz * t) * Env(t, 0.001f, decay);

        // A glassy bell: a few inharmonic partials.
        static float Bell(float t, float hz)
        {
            if (t < 0f)
                return 0f;
            float s = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-6f * t)
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * hz * 2.76f * t) * Mathf.Exp(-10f * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * hz * 5.4f * t) * Mathf.Exp(-16f * t);
            return s * Mathf.Min(1f, t / 0.003f);
        }

        // Exponential sweep from `from` to `to` Hz over `seconds`, phase-continuous.
        static float Sweep(float t, float from, float to, float seconds)
        {
            if (t < 0f)
                return 0f;
            float k = Mathf.Log(to / from) / seconds;
            float u = Mathf.Min(t, seconds);
            float phase = from * (Mathf.Exp(k * u) - 1f) / k + (t - u) * to;
            return Mathf.Sin(2f * Mathf.PI * phase);
        }

        // Soft square-ish tone for the "no" sound.
        static float Buzz(float t, float hz)
        {
            if (t < 0f)
                return 0f;
            float p = 2f * Mathf.PI * hz * t;
            return Mathf.Sin(p) + 0.3f * Mathf.Sin(3f * p) + 0.12f * Mathf.Sin(5f * p);
        }

        static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
