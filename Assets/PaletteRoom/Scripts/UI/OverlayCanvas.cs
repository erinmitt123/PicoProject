using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Keeps a world-space canvas visible through the real room. The passthrough occluder and scene mesh
    /// write the room's depth, so a panel slightly inside a wall or behind the sofa would be cut away; every
    /// graphic on this canvas is switched to an always-on-top copy of its material (see
    /// <see cref="UIKit.OverlayOf"/>), checked again now and then for graphics added later.
    /// Windows (canvases with a <see cref="UIKit.Window"/> background) also pop in with a quick scale and
    /// fade and make a soft sound as they open and close.
    /// </summary>
    [DisallowMultipleComponent]
    public class OverlayCanvas : MonoBehaviour
    {
        const int SweepEveryFrames = 30;
        const float PopSeconds = 0.22f, PopFromScale = 0.92f;
        const float QuietAfterStartSeconds = 1.5f;

        static bool s_Quitting;

        /// <summary>Set by <see cref="UIKit.Window"/>: this canvas is a panel the user opens and closes.</summary>
        public bool IsWindow { get; set; }

        readonly List<Graphic> m_Graphics = new List<Graphic>();
        Vector3 m_BaseScale;
        CanvasGroup m_Group;
        float m_PopStarted = float.NegativeInfinity;
        int m_NextSweep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() => s_Quitting = true;

        void Awake() => m_BaseScale = transform.localScale;

        void OnEnable()
        {
            Sweep();
            if (!IsWindow || Time.realtimeSinceStartup < QuietAfterStartSeconds)
                return;
            Sfx.Play(SfxKind.Open);
            if (m_Group == null && !TryGetComponent(out m_Group))
                m_Group = gameObject.AddComponent<CanvasGroup>();
            m_PopStarted = Time.unscaledTime;
            Pop(0f);
        }

        void OnDisable()
        {
            if (m_PopStarted > float.NegativeInfinity)
                Pop(1f);
            m_PopStarted = float.NegativeInfinity;
            if (IsWindow && !s_Quitting && Application.isPlaying && gameObject.scene.isLoaded &&
                Time.realtimeSinceStartup >= QuietAfterStartSeconds)
                Sfx.Play(SfxKind.Close);
        }

        void LateUpdate()
        {
            if (Time.frameCount >= m_NextSweep)
                Sweep();
            if (m_PopStarted > float.NegativeInfinity)
            {
                float t = (Time.unscaledTime - m_PopStarted) / PopSeconds;
                Pop(Mathf.Clamp01(t));
                if (t >= 1f)
                    m_PopStarted = float.NegativeInfinity;
            }
        }

        // 0 = just opened (a little small, transparent), 1 = settled. Eases out with a slight overshoot.
        void Pop(float t)
        {
            const float overshoot = 1.6f;
            float u = t - 1f;
            float ease = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            transform.localScale = m_BaseScale * Mathf.LerpUnclamped(PopFromScale, 1f, ease);
            if (m_Group != null)
                m_Group.alpha = Mathf.Clamp01(t * 2.5f);
        }

        /// <summary>Switches any graphic still drawing with a depth-tested material to the overlay copy.</summary>
        public void Sweep()
        {
            m_NextSweep = Time.frameCount + SweepEveryFrames;
            GetComponentsInChildren(true, m_Graphics);
            foreach (var g in m_Graphics)
            {
                if (g is TMP_SubMeshUI)
                    continue; // fallback glyphs copy their material from the parent text's
                if (g is TMP_Text text)
                {
                    var shared = text.fontSharedMaterial;
                    if (shared != null && !UIKit.IsOverlay(shared))
                        text.fontSharedMaterial = UIKit.OverlayOf(shared);
                    continue;
                }
                var m = g.material;
                if (m != null && !UIKit.IsOverlay(m))
                    g.material = UIKit.OverlayOf(m);
            }
            m_Graphics.Clear();
        }
    }
}
