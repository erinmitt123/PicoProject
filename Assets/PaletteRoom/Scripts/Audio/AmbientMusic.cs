using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Calm background music, made in code (no audio files): a warm pad drifting through six chords in
    /// D major, a soft low bass and an unhurried electric-piano melody with a little echo. It renders once
    /// on a background thread at start-up (about 48 s, looping seamlessly), then fades in. Its volume follows
    /// the Music slider in <see cref="AudioLevels"/>.
    /// With a radio in the room the music moves into it: it plays from the radio's spot (louder up close),
    /// follows it as it's moved, and the radio can be switched off. Without one it's all around the user.
    /// It dips under the tutorial's voice.
    /// </summary>
    public class AmbientMusic : MonoBehaviour
    {
        [SerializeField] float fadeInSeconds = 5f;
        [Tooltip("Loudness at full Music volume; the music sits under the chimes and the user's thoughts.")]
        [SerializeField] float baseVolume = 0.55f;
        [Tooltip("Loudness while the tutorial voice speaks, as a fraction of normal.")]
        [SerializeField] float duckedLevel = 0.35f;

        public static AmbientMusic Instance { get; private set; }

        AudioSource m_Source;
        float m_Fade, m_Duck = 1f, m_Spatial, m_RadioGain = 1f;
        Transform m_Radio;
        bool m_Ducked;

        /// <summary>False when the user switched the radio off.</summary>
        public bool RadioOn { get; private set; } = true;

        public bool HasRadio => m_Radio != null;

        public event System.Action RadioChanged;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>The radio the music plays from (null for none: music all around).</summary>
        public void SetRadio(Transform radio)
        {
            if (m_Radio == radio)
                return;
            m_Radio = radio;
            if (radio == null)
                RadioOn = true; // a removed radio doesn't leave the music off
            RadioChanged?.Invoke();
        }

        public void SetRadioOn(bool on)
        {
            if (RadioOn == on)
                return;
            RadioOn = on;
            Sfx.Play(on ? SfxKind.Open : SfxKind.Close);
            RadioChanged?.Invoke();
        }

        /// <summary>Lowers the music while someone is talking.</summary>
        public void Duck(bool ducked) => m_Ducked = ducked;

        IEnumerator Start()
        {
            AudioLevels.Ensure();
            // On its own object: it moves to the radio, and this one may be the app itself.
            var go = new GameObject("Music");
            go.transform.SetParent(transform, false);
            m_Source = go.AddComponent<AudioSource>();
            m_Source.playOnAwake = false;
            m_Source.loop = true;
            m_Source.spatialBlend = 0f; // 2D until there's a radio
            m_Source.priority = 0;
            m_Source.volume = 0f;
            m_Source.dopplerLevel = 0f;
            // From a radio: full volume within 1.5 m, easing to about a third across a room.
            m_Source.rolloffMode = AudioRolloffMode.Custom;
            m_Source.minDistance = 1.5f;
            m_Source.maxDistance = 12f;
            m_Source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.125f, 1f), new Keyframe(0.5f, 0.45f), new Keyframe(1f, 0.3f)));

            const int rate = MusicSynth.SampleRate;
            var render = Task.Run(() => MusicSynth.Render(rate));
            while (!render.IsCompleted)
                yield return null;
            if (render.IsFaulted || render.Result == null)
            {
                Debug.LogWarning($"[PaletteRoom] Music couldn't be made: {render.Exception?.GetBaseException().Message}");
                yield break;
            }

            var data = render.Result;
            var clip = AudioClip.Create("Palette Room Music", data.Length / 2, 2, rate, false);
            clip.SetData(data, 0);
            m_Source.clip = clip;
            m_Source.Play();
            Debug.Log($"[PaletteRoom] Music playing ({clip.length:F0} s loop).");
        }

        void Update()
        {
            if (m_Source == null || !m_Source.isPlaying)
                return;
            float dt = Time.unscaledDeltaTime;
            m_Fade = Mathf.MoveTowards(m_Fade, 1f, dt / Mathf.Max(0.01f, fadeInSeconds));
            m_Duck = Mathf.MoveTowards(m_Duck, m_Ducked ? duckedLevel : 1f, dt * 2.5f);
            bool radio = m_Radio != null;
            m_Spatial = Mathf.MoveTowards(m_Spatial, radio ? 1f : 0f, dt * 1.5f);
            m_RadioGain = Mathf.MoveTowards(m_RadioGain, radio && !RadioOn ? 0f : 1f, dt * 3f);
            if (radio)
                m_Source.transform.position = m_Radio.position + Vector3.up * 0.1f;
            m_Source.spatialBlend = m_Spatial;
            m_Source.volume = baseVolume * m_Fade * m_Duck * m_RadioGain * AudioLevels.MusicGain;
        }
    }

    /// <summary>The music itself: pure maths, so it can run off the main thread.</summary>
    static class MusicSynth
    {
        public const int SampleRate = 32000;
        const double ChordSeconds = 8.0;
        const double Beat = 60.0 / 64.0; // 64 bpm

        // Each chord: bass note, then the pad's voices (MIDI note numbers). D major, gently moving.
        static readonly int[][] Chords =
        {
            new[] { 38, 57, 61, 64, 66 }, // Dmaj9
            new[] { 35, 54, 57, 62, 64 }, // Bm11
            new[] { 31, 54, 57, 59, 62 }, // Gmaj9
            new[] { 33, 52, 57, 59, 61 }, // Aadd9
            new[] { 40, 55, 59, 62, 66 }, // Em9
            new[] { 33, 55, 57, 62, 64 }, // A7sus4
        };

        public static float[] Render(int rate)
        {
            double loop = Chords.Length * ChordSeconds;
            int n = (int)(loop * rate);
            var left = new float[n];
            var right = new float[n];
            var rng = new System.Random(7);

            Pad(left, right, rate, loop);
            Bass(left, right, rate, loop);
            Melody(left, right, rate, loop, rng);

            // Normalise, then interleave for the clip.
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
                peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            float gain = 0.85f / peak;
            var data = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                data[2 * i] = left[i] * gain;
                data[2 * i + 1] = right[i] * gain;
            }
            return data;
        }

        static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        // Every oscillator completes a whole number of cycles per loop, so the loop point never clicks.
        static double Snap(double hz, double loop) => Math.Max(1.0, Math.Round(hz * loop)) / loop;

        /// <summary>How much chord <paramref name="c"/> sounds at time <paramref name="t"/>: long, overlapping crossfades that wrap round the loop.</summary>
        static double ChordWeight(int c, double t, double loop, double fade)
        {
            double u = t - c * ChordSeconds;
            if (u < -loop / 2) u += loop;
            if (u >= loop / 2) u -= loop;
            double inW = Clamp01((u + fade / 2) / fade);
            double outW = Clamp01((ChordSeconds + fade / 2 - u) / fade);
            double w = Math.Min(inW, outW);
            return w * w * (3 - 2 * w); // smoothstep
        }

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

        /// <summary>Soft pad: each voice three slightly detuned sines with a faint octave, breathing slowly.</summary>
        static void Pad(float[] left, float[] right, int rate, double loop)
        {
            int n = left.Length;
            const double fade = 3.0, amp = 0.05;
            for (int c = 0; c < Chords.Length; c++)
            {
                var chord = Chords[c];
                for (int v = 1; v < chord.Length; v++)
                {
                    double f = Hz(chord[v]);
                    double f1 = Snap(f, loop), f2 = Snap(f * 1.003, loop), f3 = Snap(f * 0.997, loop), f8 = Snap(f * 2, loop);
                    double lfo = Snap(0.11 + 0.03 * v, loop);
                    double pan = (v - (chord.Length) / 2.0) / chord.Length; // spread voices left to right
                    float gl = (float)(Math.Sqrt(0.5 - pan * 0.5)), gr = (float)(Math.Sqrt(0.5 + pan * 0.5));
                    for (int i = 0; i < n; i++)
                    {
                        double t = i / (double)rate;
                        double w = ChordWeight(c, t, loop, fade);
                        if (w <= 0) continue;
                        double p = 2 * Math.PI * t;
                        double s = Math.Sin(p * f1) + 0.7 * Math.Sin(p * f2) + 0.7 * Math.Sin(p * f3) + 0.12 * Math.Sin(p * f8);
                        double breathe = 0.8 + 0.2 * Math.Sin(p * lfo + v);
                        float x = (float)(s * w * breathe * amp);
                        left[i] += x * gl;
                        right[i] += x * gr;
                    }
                }
            }
        }

        /// <summary>Round, low bass: the chord's root with a touch of its octave so headset speakers carry it.</summary>
        static void Bass(float[] left, float[] right, int rate, double loop)
        {
            int n = left.Length;
            const double fade = 2.0, amp = 0.09;
            for (int c = 0; c < Chords.Length; c++)
            {
                double f = Hz(Chords[c][0] + 12);
                double f1 = Snap(f, loop), f2 = Snap(f * 2, loop);
                for (int i = 0; i < n; i++)
                {
                    double t = i / (double)rate;
                    double w = ChordWeight(c, t, loop, fade);
                    if (w <= 0) continue;
                    double p = 2 * Math.PI * t;
                    float x = (float)((Math.Sin(p * f1) + 0.35 * Math.Sin(p * f2)) * w * amp);
                    left[i] += x;
                    right[i] += x;
                }
            }
        }

        /// <summary>
        /// An unhurried electric-piano line from each chord's notes, an octave up, on the off-beats as often
        /// as the beats, with a ping-pong echo for space.
        /// </summary>
        static void Melody(float[] left, float[] right, int rate, double loop, System.Random rng)
        {
            int n = left.Length;
            var dryL = new float[n];
            var dryR = new float[n];
            double noteSeconds = 3.2;
            int noteLength = (int)(noteSeconds * rate);
            int slotsPerChord = (int)Math.Round(ChordSeconds / (Beat / 2));
            int lastNote = -1;

            for (int c = 0; c < Chords.Length; c++)
            {
                var chord = Chords[c];
                for (int slot = 0; slot < slotsPerChord; slot++)
                {
                    // Sparse: more likely on the beat, rarely two in a row.
                    double chance = slot % 2 == 0 ? 0.42 : 0.18;
                    if (rng.NextDouble() > chance)
                        continue;
                    int note;
                    do note = chord[1 + rng.Next(chord.Length - 1)] + 12;
                    while (note == lastNote);
                    lastNote = note;

                    double f = Hz(note);
                    double velocity = 0.55 + 0.45 * rng.NextDouble();
                    double pan = (rng.NextDouble() - 0.5) * 0.7;
                    float gl = (float)Math.Sqrt(0.5 - pan * 0.5), gr = (float)Math.Sqrt(0.5 + pan * 0.5);
                    int start = (int)((c * ChordSeconds + slot * Beat / 2) * rate);
                    for (int j = 0; j < noteLength; j++)
                    {
                        double t = j / (double)rate;
                        double p = 2 * Math.PI * f * t;
                        double env = Math.Min(1.0, t / 0.006) * Math.Exp(-1.6 * t);
                        // A gentle FM bell: bright at the strike, mellowing as it rings.
                        double s = Math.Sin(p + 1.1 * Math.Exp(-5 * t) * Math.Sin(p)) + 0.18 * Math.Sin(2 * p) * Math.Exp(-4 * t);
                        float x = (float)(s * env * velocity * 0.11);
                        int i = (start + j) % n; // tails wrap round to the start of the loop
                        dryL[i] += x * gl;
                        dryR[i] += x * gr;
                    }
                }
            }

            // Ping-pong echo, run round the loop three times so the tail wrapping into the start is already there.
            int delay = (int)(Beat * 0.75 * rate);
            var bufL = new float[delay];
            var bufR = new float[delay];
            const float feedback = 0.38f, wet = 0.32f;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    int k = (int)(((long)pass * n + i) % delay); // continuous across passes, so the echo time never jumps
                    float dl = bufL[k], dr = bufR[k];
                    bufL[k] = (dryL[i] + dryR[i]) * 0.5f + dr * feedback;
                    bufR[k] = dl * feedback;
                    if (pass == 2)
                    {
                        left[i] += dryL[i] + dl * wet;
                        right[i] += dryR[i] + dr * wet;
                    }
                }
            }
        }
    }
}
