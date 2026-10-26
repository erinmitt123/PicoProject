using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Holds the active palette and restyles every registered furniture item with the 60-30-10 rule:
    /// Dominant role -> dominant color, Secondary -> secondary, Accent -> accent alternating with highlight.
    /// </summary>
    public class PaletteManager : MonoBehaviour
    {
        public const float BlendDuration = 0.4f;

        public static PaletteManager Instance { get; private set; }

        readonly List<FurnitureItem> m_Items = new List<FurnitureItem>();
        ColorPalette m_Current;

        public ColorPalette Current => m_Current ?? PalettePresets.Default;
        public IReadOnlyList<FurnitureItem> Items => m_Items;

        readonly List<ColorPalette> m_Customs = new List<ColorPalette>();

        /// <summary>Palettes the user made on the color wheel, oldest first.</summary>
        public IReadOnlyList<ColorPalette> Customs => m_Customs;

        /// <summary>The user's most recent palette, or null until they make one.</summary>
        public ColorPalette Custom => m_Customs.Count > 0 ? m_Customs[m_Customs.Count - 1] : null;

        public event Action<ColorPalette> PaletteChanged;
        /// <summary>Raised when the user saves a new palette.</summary>
        public event Action<ColorPalette> CustomPaletteChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PaletteRoom] Duplicate PaletteManager disabled.", this);
                enabled = false;
                return;
            }
            Instance = this;
            m_Current = PalettePresets.Default;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(FurnitureItem item)
        {
            if (item == null || m_Items.Contains(item))
                return;
            m_Items.Add(item);
            // A newly added item takes the current look straight away; it is not a palette change.
            ApplyToItem(item, m_Items.Count - 1, Current, animate: false);
        }

        public void Unregister(FurnitureItem item) => m_Items.Remove(item);

        /// <summary>Saves <paramref name="palette"/> alongside the presets as "My palette N" and restyles the room with it.</summary>
        public void SetCustomPalette(ColorPalette palette)
        {
            if (palette == null)
                return;
            var saved = palette.Clone($"My palette {m_Customs.Count + 1}");
            m_Customs.Add(saved);
            CustomPaletteChanged?.Invoke(saved);
            SetPalette(saved);
        }

        /// <summary>
        /// Switches palettes. With a <see cref="PaletteWave"/> in the scene the colors ripple out from the
        /// user's hand piece by piece; otherwise every paintable zone blends over <see cref="BlendDuration"/>.
        /// </summary>
        public void SetPalette(ColorPalette palette)
        {
            if (palette == null)
                return;
            m_Current = palette;
            if (PaletteWave.Instance != null && PaletteWave.Instance.isActiveAndEnabled && m_Items.Count > 0)
            {
                PaletteWave.Instance.Play(m_Items, palette, item =>
                {
                    // Applied with the palette current at that moment, so a newer pick always wins.
                    int index = m_Items.IndexOf(item);
                    if (index >= 0)
                        ApplyToItem(item, index, Current, animate: true);
                });
            }
            else
            {
                for (int i = 0; i < m_Items.Count; i++)
                    ApplyToItem(m_Items[i], i, palette, animate: true);
            }
            PaletteChanged?.Invoke(palette);
        }

        void ApplyToItem(FurnitureItem item, int index, ColorPalette palette, bool animate)
        {
            if (item == null)
                return;
            var roleSlot = RoleSlot(item.Role, AccentOrdinal(index));
            foreach (var zone in item.Zones)
            {
                if (!zone.FollowsPalette)
                    continue;
                // A color the user picked for this part wins over the 60-30-10 assignment.
                var slot = zone.Override ?? (zone.Source == ZoneColorSource.ItemRole ? roleSlot : zone.Slot);
                var color = palette.Get(slot);
                if (animate)
                    zone.BlendTo(color, BlendDuration);
                else
                    zone.SetColorImmediate(color);
            }
        }

        /// <summary>Recolors one item after its per-part picks changed (blends, like a palette change).</summary>
        public void RefreshItem(FurnitureItem item)
        {
            int index = m_Items.IndexOf(item);
            if (index >= 0)
                ApplyToItem(item, index, Current, animate: true);
        }

        /// <summary>The palette slot a part shows right now (its pick, or what 60-30-10 gives it).</summary>
        public PaletteSlot? SlotFor(FurnitureItem item, PaintableZone zone)
        {
            if (zone.Override.HasValue)
                return zone.Override;
            if (!zone.IsPaintable)
                return null;
            int index = m_Items.IndexOf(item);
            return zone.Source == ZoneColorSource.ItemRole ? RoleSlot(item.Role, AccentOrdinal(Mathf.Max(0, index))) : zone.Slot;
        }

        /// <summary>How many Accent items were registered before the item at <paramref name="index"/>.</summary>
        int AccentOrdinal(int index)
        {
            int ordinal = 0;
            for (int i = 0; i < index; i++)
            {
                if (m_Items[i] != null && m_Items[i].Role == FurnitureRole.Accent)
                    ordinal++;
            }
            return ordinal;
        }

        // Accent pieces alternate accent / highlight so small items don't all match.
        static PaletteSlot RoleSlot(FurnitureRole role, int accentOrdinal)
        {
            switch (role)
            {
                case FurnitureRole.Dominant: return PaletteSlot.Dominant;
                case FurnitureRole.Secondary: return PaletteSlot.Secondary;
                default: return accentOrdinal % 2 == 0 ? PaletteSlot.Accent : PaletteSlot.Highlight;
            }
        }
    }
}
