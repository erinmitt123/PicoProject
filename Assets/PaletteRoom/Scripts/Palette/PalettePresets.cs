using System.Collections.Generic;

namespace PaletteRoom
{
    /// <summary>
    /// The eight built-in palettes, each built on a color-wheel harmony (hues measured on the RYB painter's
    /// wheel) and weighted for 60-30-10: a muted dominant, a supporting secondary, and saturated accents.
    /// Order matches the palette dock.
    /// </summary>
    public static class PalettePresets
    {
        //                                 name                 harmony                          dominant   secondary  accent     neutral    highlight
        public static readonly IReadOnlyList<ColorPalette> All = new[]
        {
            // Dusty blue against warm amber, softened by a warm-white dominant.
            new ColorPalette("Scandinavian",      HarmonyRule.Complementary,      "#DCD6CC", "#8A9FBF", "#D98E4A", "#F4F1EB", "#3E5566"),
            // Sapphire, ruby and topaz: the blue–red–yellow primary triad in deep jewel values.
            new ColorPalette("Moody Jewel",       HarmonyRule.Triadic,            "#1F3550", "#6E2338", "#D4A23A", "#2B2B30", "#3D6A99"),
            // Sea blues split against coral and sand, the two hues either side of blue's complement.
            new ColorPalette("Coastal",           HarmonyRule.SplitComplementary, "#E8E1D2", "#7FA7C2", "#2F5D8A", "#F7F6F2", "#E07A5F"),
            // Neighbouring red-oranges through ochre: warm and enveloping.
            new ColorPalette("Terracotta Desert", HarmonyRule.Analogous,          "#D9B99B", "#B5653F", "#D99A2B", "#F1E6D8", "#8A3B24"),
            // Deep teal opposite burnt orange, with mustard and brick riding the warm side.
            new ColorPalette("Mid-Century",       HarmonyRule.Complementary,      "#2B5A66", "#C46A2B", "#E2B13C", "#EFE7DA", "#8B3A2F"),
            // One sage hue in tints and shades.
            new ColorPalette("Monochrome Sage",   HarmonyRule.Monochromatic,      "#9CAF88", "#7D9170", "#4F6146", "#E9EDE4", "#C5D3B8"),
            // Oat, taupe and clay sit side by side on the warm wheel; charcoal grounds them.
            new ColorPalette("Japandi",           HarmonyRule.Analogous,          "#CFC6B8", "#8C7B6B", "#2F2F2F", "#F2EEE7", "#B07D62"),
            // Coral, sun yellow and violet-blue: a playful triad over a peach base.
            new ColorPalette("Sunset Pop",        HarmonyRule.Triadic,            "#F6C1A6", "#F28C8C", "#5B4FC2", "#FFF4EC", "#FFD93C"),
        };

        public static ColorPalette Default => All[0];
    }
}
