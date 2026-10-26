using UnityEngine;

namespace PaletteRoom
{
    /// <summary>Plain-words color names ("warm white", "dark navy", "terracotta") for people rather than hex codes.</summary>
    public static class ColorNames
    {
        public static string Describe(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            string tone = v < 0.25f ? "dark " : v > 0.85f && s < 0.15f ? "" : v > 0.7f ? "light " : "";
            float deg = h * 360f;
            if (s < 0.12f)
            {
                // Near-neutrals still lean warm or cool; say so (a cream sofa isn't "white").
                bool tinted = s > 0.035f;
                bool warm = deg < 70f || deg > 330f;
                if (v > 0.85f)
                    return tinted ? (warm ? "warm white" : "cool white") : "white";
                if (v < 0.25f)
                    return tinted && warm ? "espresso" : "black";
                return tone + (tinted ? (warm ? "greige" : "cool grey") : "grey");
            }
            string hue = deg < 15f || deg >= 345f ? "red" : deg < 40f ? (v < 0.6f ? "brown" : "orange") : deg < 65f ? (s < 0.35f ? "beige" : "yellow")
                : deg < 160f ? "green" : deg < 200f ? "teal" : deg < 255f ? "blue" : deg < 290f ? "purple" : "pink";
            if (s < 0.3f && (hue == "orange" || hue == "yellow" || hue == "brown"))
                hue = v > 0.6f ? "beige" : "taupe";
            if (hue == "orange" && s > 0.45f && v < 0.8f)
                hue = "terracotta";
            if (hue == "blue" && v < 0.45f)
                hue = "navy";
            return tone + hue;
        }

        public static string Describe(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? Describe(c) : hex;
    }
}
