using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PaletteRoom
{
    /// <summary>
    /// The Palette Wave: when a palette is picked, the new colors ripple outward from the hand that
    /// picked it. A ring in the palette's accent color spreads across the floor, and each piece changes
    /// as the ring reaches it, with a small pop and a spatial chime from where it stands. Chimes climb a
    /// pentatonic scale, so the wave plays as a rising phrase rather than a clatter.
    /// </summary>
    public class PaletteWave : MonoBehaviour
    {
        const float Speed = 2.2f;           // metres per second across the room
        const float MaxRadius = 7f;
        const float PopScale = 0.05f;       // how much a piece swells as the color lands
        const float PopSeconds = 0.35f;

        // C major pentatonic over two octaves: any run of these sounds consonant.
        static readonly float[] Scale = { 0f, 2f, 4f, 7f, 9f, 12f, 14f, 16f, 19f, 21f, 24f };

        public static PaletteWave Instance { get; private set; }

        readonly InteractorPoints m_Points = new InteractorPoints();
        readonly List<AudioSource> m_Voices = new List<AudioSource>();
        Coroutine m_Wave;
        AudioClip m_Chime;
        LineRenderer m_Ring;
        Transform m_Head;
        Material m_RingMaterial;

        public void Configure(Transform head, Material ringMaterial)
        {
            m_Head = head;
            m_RingMaterial = ringMaterial;
        }

        void Awake()
        {
            Instance = this;
            m_Chime = MakeChime();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Recolors <paramref name="items"/> to <paramref name="palette"/> in a wave from the hand (or the
        /// head, when nothing is pointing). <paramref name="apply"/> recolors one item.
        /// </summary>
        public void Play(IReadOnlyList<FurnitureItem> items, ColorPalette palette, System.Action<FurnitureItem> apply)
        {
            if (m_Wave != null)
                StopCoroutine(m_Wave);
            var origin = PointerOrigin(out var hand);
            hand?.SendHapticImpulse(0.25f, 0.05f); // the wave leaves your hand
            m_Wave = StartCoroutine(Wave(new List<FurnitureItem>(items), palette, apply, origin));
        }

        /// <summary>Where the user just touched the UI with a ray, poke or pinch; else just in front of their head.</summary>
        Vector3 PointerOrigin(out XRBaseInputInteractor hand)
        {
            hand = null;
            foreach (var interactor in m_Points.Active)
            {
                if (interactor.isActiveAndEnabled && interactor.TryGetCurrentUIRaycastResult(out var hit) && hit.isValid)
                {
                    hand = interactor;
                    return hit.worldPosition;
                }
            }
            var head = m_Head != null ? m_Head : Camera.main != null ? Camera.main.transform : transform;
            return head.position + head.forward * 0.4f;
        }

        IEnumerator Wave(List<FurnitureItem> items, ColorPalette palette, System.Action<FurnitureItem> apply, Vector3 origin)
        {
            // Nearest first. Distance is measured to each piece's middle so tall and flat pieces feel fair.
            items.RemoveAll(i => i == null);
            items.Sort((a, b) => Distance(origin, a).CompareTo(Distance(origin, b)));
            float floorY = items.Count > 0 && items[0].transform.parent != null ? items[0].transform.parent.position.y : 0f;
            StartRing(new Vector3(origin.x, floorY + 0.01f, origin.z), palette.accent);

            float start = Time.time;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;
                float due = start + Mathf.Min(Distance(origin, item), MaxRadius) / Speed;
                while (Time.time < due)
                    yield return null;
                apply(item);
                StartCoroutine(Pop(item.transform));
                Chime(Centre(item), i, items.Count);
            }
            m_Wave = null;
        }

        /// <summary>The wave's per-piece flourish — a pop and a chime pitched by position in a run — for other moments (the reveal).</summary>
        public void Accent(FurnitureItem item, int index, int count)
        {
            if (item == null)
                return;
            StartCoroutine(Pop(item.transform));
            Chime(Centre(item), index, count);
        }

        static float Distance(Vector3 origin, FurnitureItem item) => Vector3.Distance(origin, Centre(item));

        static Vector3 Centre(FurnitureItem item)
        {
            var box = item.GetComponent<BoxCollider>();
            return box != null ? item.transform.TransformPoint(box.center) : item.transform.position;
        }

        IEnumerator Pop(Transform piece)
        {
            // Swell and settle without fighting anything else that owns the scale (e.g. the drop-in).
            var rest = piece.localScale;
            if (rest.sqrMagnitude < 0.5f)
                yield break; // still dropping in
            for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
            {
                if (piece == null)
                    yield break;
                float k = Mathf.Sin(t / PopSeconds * Mathf.PI);
                piece.localScale = rest * (1f + PopScale * k);
                yield return null;
            }
            if (piece != null)
                piece.localScale = rest;
        }

        // ---- the ring -------------------------------------------------------------------------

        void StartRing(Vector3 centre, Color color)
        {
            if (m_Ring == null)
            {
                var go = new GameObject("Palette Wave Ring");
                go.transform.SetParent(transform, false);
                m_Ring = go.AddComponent<LineRenderer>();
                m_Ring.loop = true;
                m_Ring.useWorldSpace = true;
                m_Ring.positionCount = 96;
                m_Ring.numCornerVertices = 2;
                m_Ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Ring.receiveShadows = false;
                if (m_RingMaterial != null)
                    m_Ring.material = new Material(m_RingMaterial) { name = "Palette Wave Ring" };
            }
            if (m_Ring.material != null)
            {
                m_Ring.material.SetColor("_BaseColor", color);
                if (m_Ring.material.HasProperty("_EmissionColor"))
                {
                    m_Ring.material.EnableKeyword("_EMISSION");
                    m_Ring.material.SetColor("_EmissionColor", color * 0.8f);
                }
            }
            StartCoroutine(Ripple(centre));
        }

        IEnumerator Ripple(Vector3 centre)
        {
            m_Ring.enabled = true;
            float duration = MaxRadius / Speed;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float radius = t * Speed;
                float fade = 1f - t / duration;
                m_Ring.widthMultiplier = Mathf.Lerp(0.005f, 0.06f, fade);
                for (int i = 0; i < m_Ring.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / m_Ring.positionCount;
                    m_Ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                }
                yield return null;
            }
            m_Ring.enabled = false;
        }

        // ---- chimes -------------------------------------------------------------------------

        void Chime(Vector3 at, int index, int count)
        {
            var voice = FreeVoice();
            voice.transform.position = at;
            // Climb the scale as the wave travels; long waves use the whole two octaves.
            int step = count <= 1 ? 0 : Mathf.RoundToInt(index / (float)(count - 1) * (Scale.Length - 1));
            voice.pitch = Mathf.Pow(2f, Scale[Mathf.Clamp(step, 0, Scale.Length - 1)] / 12f);
            voice.volume = 0.55f * AudioLevels.EffectsGain;
            voice.PlayOneShot(m_Chime);
        }

        AudioSource FreeVoice()
        {
            foreach (var v in m_Voices)
                if (!v.isPlaying)
                    return v;
            var go = new GameObject($"Chime Voice {m_Voices.Count + 1}");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;            // fully 3D: the chime comes from the piece
            source.spatialize = true;            // uses the platform spatializer when one is set
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.6f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            m_Voices.Add(source);
            return source;
        }

        /// <summary>A soft glass-bell tone made in code: a few inharmonic partials with a quick decay.</summary>
        static AudioClip MakeChime()
        {
            const int rate = 44100;
            const float seconds = 0.9f, baseHz = 523.25f; // C5
            int n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            var partials = new[] { (1f, 1f, 3.2f), (2.76f, 0.45f, 5.5f), (5.4f, 0.22f, 8f), (8.93f, 0.1f, 11f) };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Clamp01(t / 0.004f);
                float s = 0f;
                foreach (var (ratio, amp, decay) in partials)
                    s += amp * Mathf.Exp(-decay * t) * Mathf.Sin(2f * Mathf.PI * baseHz * ratio * t);
                data[i] = s * attack * 0.45f;
            }
            var clip = AudioClip.Create("Palette Chime", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
