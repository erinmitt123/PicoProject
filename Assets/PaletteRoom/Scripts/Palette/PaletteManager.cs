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

        /// <summary>The user's palette from the color wheel, or null until they make one.</summary>
        public ColorPalette Custom { get; private set; }

        public event Action<ColorPalette> PaletteChanged;
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

        /// <summary>Stores <paramref name="palette"/> as the user's palette and restyles the room with it.</summary>
        public void SetCustomPalette(ColorPalette palette)
        {
            if (palette == null)
                return;
            Custom = palette.Clone("My palette");
            CustomPaletteChanged?.Invoke(Custom);
            SetPalette(Custom);
        }

        /// <summary>Switches palettes; every paintable zone blends over <see cref="BlendDuration"/>.</summary>
        public void SetPalette(ColorPalette palette)
        {
            if (palette == null)
                return;
            m_Current = palette;
            for (int i = 0; i < m_Items.Count; i++)
                ApplyToItem(m_Items[i], i, palette, animate: true);
            PaletteChanged?.Invoke(palette);
        }

        void ApplyToItem(FurnitureItem item, int index, ColorPalette palette, bool animate)
        {
            if (item == null)
                return;
            var roleSlot = RoleSlot(item.Role, AccentOrdinal(index));
            foreach (var zone in item.Zones)
            {
                if (!zone.IsPaintable)
                    continue;
                var slot = zone.Source == ZoneColorSource.ItemRole ? roleSlot : zone.Slot;
                var color = palette.Get(slot);
                if (animate)
                    zone.BlendTo(color, BlendDuration);
                else
                    zone.SetColorImmediate(color);
            }
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
