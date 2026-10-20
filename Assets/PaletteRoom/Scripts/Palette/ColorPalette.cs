using System;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>The five color slots every palette defines.</summary>
    public enum PaletteSlot
    {
        Dominant,
        Secondary,
        Accent,
        Neutral,
        Highlight,
    }

    /// <summary>A named set of five colors used to restyle the room.</summary>
    [Serializable]
    public class ColorPalette
    {
        public string name;
        public HarmonyRule harmony;
        public Color dominant;
        public Color secondary;
        public Color accent;
        public Color neutral;
        public Color highlight;

        public ColorPalette(string name, HarmonyRule harmony, string dominant, string secondary, string accent, string neutral, string highlight)
            : this(name, harmony, Hex(dominant), Hex(secondary), Hex(accent), Hex(neutral), Hex(highlight))
        {
        }

        public ColorPalette(string name, HarmonyRule harmony, Color dominant, Color secondary, Color accent, Color neutral, Color highlight)
        {
            this.name = name;
            this.harmony = harmony;
            this.dominant = dominant;
            this.secondary = secondary;
            this.accent = accent;
            this.neutral = neutral;
            this.highlight = highlight;
        }

        public ColorPalette Clone(string newName = null) =>
            new ColorPalette(newName ?? name, harmony, dominant, secondary, accent, neutral, highlight);

        public Color Get(PaletteSlot slot)
        {
            switch (slot)
            {
                case PaletteSlot.Dominant: return dominant;
                case PaletteSlot.Secondary: return secondary;
                case PaletteSlot.Accent: return accent;
                case PaletteSlot.Neutral: return neutral;
                case PaletteSlot.Highlight: return highlight;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        public void Set(PaletteSlot slot, Color color)
        {
            switch (slot)
            {
                case PaletteSlot.Dominant: dominant = color; break;
                case PaletteSlot.Secondary: secondary = color; break;
                case PaletteSlot.Accent: accent = color; break;
                case PaletteSlot.Neutral: neutral = color; break;
                case PaletteSlot.Highlight: highlight = color; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c))
                throw new ArgumentException($"Invalid palette color '{hex}'");
            return c;
        }
    }
}
