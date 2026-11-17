using System;
using System.Collections;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>Where a zone takes its color from when a palette is applied.</summary>
    public enum ZoneColorSource
    {
        /// <summary>Follows the owning item's role (60-30-10 auto-apply).</summary>
        ItemRole,
        /// <summary>Always uses a specific palette slot (e.g. a pouf cover in Accent).</summary>
        Slot,
        /// <summary>Never repainted (e.g. wooden legs, metal frames, plant leaves).</summary>
        Fixed,
    }

    /// <summary>
    /// A named, independently colorable part of a furniture item. All of its material slots share one
    /// material instance, so a blend touches a single material. A slot can be a whole renderer or one
    /// sub-mesh of it, so a single model can hold fabric and wood in separate zones.
    /// </summary>
    public class PaintableZone : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>A renderer plus the material slot this zone owns (-1 = every slot).</summary>
        [Serializable]
        public struct Target
        {
            public Renderer renderer;
            public int materialIndex;

            public Target(Renderer renderer, int materialIndex = -1)
            {
                this.renderer = renderer;
                this.materialIndex = materialIndex;
            }
        }

        [SerializeField] string zoneName = "Body";
        [SerializeField] ZoneColorSource source = ZoneColorSource.ItemRole;
        [SerializeField] PaletteSlot slot = PaletteSlot.Neutral;
        [SerializeField] Target[] targets = Array.Empty<Target>();
        [Tooltip("Whether the user may pick this part's color (off for bulbs, glass, soil, leaves).")]
        [SerializeField] bool allowOverride = true;

        Material m_Material;
        Coroutine m_Blend;
        bool m_HasOverride;
        PaletteSlot m_Override;
        Color m_OriginalColor = Color.white;

        public string ZoneName => zoneName;
        public ZoneColorSource Source => source;
        public PaletteSlot Slot => slot;
        public bool IsPaintable => source != ZoneColorSource.Fixed;
        public Target[] Targets => targets;
        public bool AllowOverride => allowOverride;

        /// <summary>The palette slot the user pinned this part to, or null to follow the 60-30-10 rule.</summary>
        public PaletteSlot? Override => m_HasOverride ? m_Override : (PaletteSlot?)null;

        /// <summary>Whether a palette change recolors this zone (it's paintable, or the user pinned a color).</summary>
        public bool FollowsPalette => IsPaintable || m_HasOverride;

        /// <summary>
        /// Pins this part to a palette slot (it then follows that slot through palette changes), or clears
        /// the pin. A fixed part (e.g. oak legs) returns to its natural finish when cleared.
        /// </summary>
        public void SetOverride(PaletteSlot? value, float blendDuration)
        {
            if (!allowOverride)
                return;
            m_HasOverride = value.HasValue;
            if (value.HasValue)
                m_Override = value.Value;
            EnsureMaterial();
            if (!m_HasOverride && !IsPaintable)
                BlendTo(m_OriginalColor, blendDuration);
        }

        public void SetAllowOverride(bool allowed) => allowOverride = allowed;
        public Color CurrentColor => m_Material != null ? m_Material.GetColor(BaseColorId) : Color.white;

        /// <summary>Sets up the zone with a material it owns (used by code-built furniture).</summary>
        public void Configure(string zoneName, ZoneColorSource source, PaletteSlot slot, Material material, Target[] targets)
        {
            this.zoneName = zoneName;
            this.source = source;
            this.slot = slot;
            this.targets = targets;
            m_Material = material;
            m_OriginalColor = material.GetColor(BaseColorId);
            Assign(material);
        }

        /// <summary>Sets up the zone on slots that already hold the right material (used for prefabs).</summary>
        public void ConfigureExisting(string zoneName, ZoneColorSource source, PaletteSlot slot, Target[] targets)
        {
            this.zoneName = zoneName;
            this.source = source;
            this.slot = slot;
            this.targets = targets;
        }

        void Awake()
        {
            // Prefab zones share the asset material; give each placed item its own copy so pieces can
            // be colored independently. Fixed zones keep the shared original until the user paints them.
            if (IsPaintable)
                EnsureMaterial();
        }

        void EnsureMaterial()
        {
            if (m_Material != null || targets.Length == 0 || targets[0].renderer == null)
                return;
            var t = targets[0];
            var shared = t.materialIndex < 0 ? t.renderer.sharedMaterial : t.renderer.sharedMaterials[t.materialIndex];
            if (shared == null)
                return;
            m_Material = new Material(shared) { name = shared.name + " (zone)" };
            m_OriginalColor = m_Material.GetColor(BaseColorId);
            Assign(m_Material);
        }

        void Assign(Material material)
        {
            foreach (var t in targets)
            {
                if (t.renderer == null)
                    continue;
                if (t.materialIndex < 0)
                {
                    var all = t.renderer.sharedMaterials;
                    for (int i = 0; i < all.Length; i++)
                        all[i] = material;
                    t.renderer.sharedMaterials = all;
                }
                else
                {
                    var mats = t.renderer.sharedMaterials;
                    mats[t.materialIndex] = material;
                    t.renderer.sharedMaterials = mats;
                }
            }
        }

        public void SetColorImmediate(Color color)
        {
            if (m_Blend != null)
            {
                StopCoroutine(m_Blend);
                m_Blend = null;
            }
            if (m_Material != null)
                m_Material.SetColor(BaseColorId, color);
        }

        /// <summary>Animates to <paramref name="target"/>. Color changes never snap.</summary>
        public void BlendTo(Color target, float duration)
        {
            if (m_Material == null)
                return;
            if (!isActiveAndEnabled || duration <= 0f)
            {
                SetColorImmediate(target);
                return;
            }
            if (m_Blend != null)
                StopCoroutine(m_Blend);
            m_Blend = StartCoroutine(BlendRoutine(CurrentColor, target, duration));
        }

        IEnumerator BlendRoutine(Color from, Color to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                m_Material.SetColor(BaseColorId, Color.Lerp(from, to, k));
                yield return null;
            }
            m_Material.SetColor(BaseColorId, to);
            m_Blend = null;
        }

        void OnDestroy()
        {
            if (m_Material == null)
                return;
            if (Application.isPlaying)
                Destroy(m_Material);
            else
                DestroyImmediate(m_Material);
        }
    }
}
