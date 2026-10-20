using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>Classic color-wheel harmony rules.</summary>
    public enum HarmonyRule
    {
        /// <summary>Opposite hues (180°): high contrast, energetic.</summary>
        Complementary,
        /// <summary>Base plus the two hues beside its complement (±150°): contrast with less tension.</summary>
        SplitComplementary,
        /// <summary>Neighbouring hues (±30°): calm and cohesive.</summary>
        Analogous,
        /// <summary>Three evenly spaced hues (120°): vibrant but balanced.</summary>
        Triadic,
        /// <summary>Two complementary pairs (rectangle: 0°, 60°, 180°, 240°): rich, needs a clear dominant.</summary>
        Tetradic,
        /// <summary>One hue in several tints and shades: quiet and sophisticated.</summary>
        Monochromatic,
    }

    /// <summary>
    /// Generates 60-30-10 palettes from one base color using color-wheel harmony rules.
    /// Hue relationships are measured on the painter's RYB wheel (red–yellow–blue), which is what
    /// interior designers use: there blue's complement is orange and red's is green, whereas the
    /// screen RGB wheel would pair blue with yellow.
    /// </summary>
    public static class ColorHarmony
    {
        public static readonly HarmonyRule[] AllRules =
        {
            HarmonyRule.Complementary,
            HarmonyRule.SplitComplementary,
            HarmonyRule.Analogous,
            HarmonyRule.Triadic,
            HarmonyRule.Tetradic,
            HarmonyRule.Monochromatic,
        };

        public static string DisplayName(HarmonyRule rule)
        {
            switch (rule)
            {
                case HarmonyRule.SplitComplementary: return "Split-complementary";
                default: return rule.ToString();
            }
        }

        // Matching hue anchors on the two wheels (degrees): red, orange, yellow, green, blue, violet, red.
        static readonly float[] RybAnchors = { 0f, 60f, 120f, 180f, 240f, 300f, 360f };
        static readonly float[] RgbAnchors = { 0f, 30f, 60f, 120f, 240f, 280f, 360f };

        /// <summary>Screen (RGB/HSV) hue in degrees → painter's (RYB) hue in degrees.</summary>
        public static float RgbToRybHue(float rgbDegrees) => Remap(Wrap(rgbDegrees), RgbAnchors, RybAnchors);

        /// <summary>Painter's (RYB) hue in degrees → screen (RGB/HSV) hue in degrees.</summary>
        public static float RybToRgbHue(float rybDegrees) => Remap(Wrap(rybDegrees), RybAnchors, RgbAnchors);

        /// <summary>Builds a color from an RYB hue (degrees) plus HSV saturation and value.</summary>
        public static Color FromRyb(float rybDegrees, float saturation, float value)
        {
            return Color.HSVToRGB(RybToRgbHue(rybDegrees) / 360f, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
        }

        /// <summary>Splits a color into RYB hue (degrees), saturation and value.</summary>
        public static void ToRyb(Color color, out float rybDegrees, out float saturation, out float value)
        {
            Color.RGBToHSV(color, out float h, out saturation, out value);
            rybDegrees = RgbToRybHue(h * 360f);
        }

        /// <summary>
        /// The picked color stays the dominant (60%) color; secondary (30%) and accent (10%) come from the
        /// rule, accents get extra saturation so they read at small sizes, and the neutral is a soft
        /// tint of the base so walls and trim feel related.
        /// </summary>
        public static ColorPalette Generate(Color baseColor, HarmonyRule rule, string name = null)
        {
            ToRyb(baseColor, out float h, out float s, out float v);

            // Accents need enough chroma to be noticed; deep shades need enough value to read in passthrough.
            float accentS = Mathf.Max(s, 0.55f);
            float accentV = Mathf.Clamp(Mathf.Max(v, 0.75f), 0.6f, 0.92f); // bright enough to pop against a dark base
            float shadeV = Mathf.Clamp(v * 0.6f, 0.22f, 0.55f);
            Color neutral = FromRyb(h, Mathf.Min(s, 0.07f), 0.95f);

            Color secondary, accent, highlight;
            switch (rule)
            {
                case HarmonyRule.Complementary:
                    secondary = FromRyb(h, s * 0.8f, v < 0.5f ? v + 0.3f : v - 0.25f);
                    accent = FromRyb(h + 180f, accentS, accentV);
                    highlight = FromRyb(h + 180f, accentS * 0.8f, shadeV);
                    break;
                case HarmonyRule.SplitComplementary:
                    secondary = FromRyb(h + 150f, s * 0.6f, v);
                    accent = FromRyb(h + 210f, accentS, accentV);
                    highlight = FromRyb(h + 150f, accentS, shadeV);
                    break;
                case HarmonyRule.Analogous:
                    secondary = FromRyb(h + 30f, s * 0.85f, v);
                    accent = FromRyb(h - 30f, accentS, accentV);
                    highlight = FromRyb(h + 60f, accentS * 0.85f, Mathf.Clamp(v * 0.8f, 0.3f, 0.8f));
                    break;
                case HarmonyRule.Triadic:
                    secondary = FromRyb(h + 120f, s * 0.6f, v);
                    accent = FromRyb(h + 240f, accentS, accentV);
                    highlight = FromRyb(h + 120f, accentS, shadeV);
                    break;
                case HarmonyRule.Tetradic:
                    secondary = FromRyb(h + 60f, s * 0.7f, v);
                    accent = FromRyb(h + 180f, accentS, accentV);
                    highlight = FromRyb(h + 240f, accentS * 0.85f, shadeV);
                    break;
                default: // Monochromatic
                    secondary = FromRyb(h, s * 0.6f, v > 0.5f ? v - 0.3f : v + 0.3f);
                    accent = FromRyb(h, Mathf.Min(1f, s + 0.3f), Mathf.Clamp(v * 0.65f, 0.2f, 0.7f));
                    highlight = FromRyb(h, s * 0.35f, Mathf.Clamp(v + 0.2f, 0.4f, 0.95f));
                    break;
            }

            return new ColorPalette(name ?? DisplayName(rule), rule, baseColor, secondary, accent, neutral, highlight);
        }

        // ---- multi-color suggestions ------------------------------------------------------

        /// <summary>A color the user pinned to a slot. Higher <see cref="order"/> = set more recently.</summary>
        public struct SetColor
        {
            public PaletteSlot slot;
            public Color color;
            public int order;

            public SetColor(PaletteSlot slot, Color color, int order)
            {
                this.slot = slot;
                this.color = color;
                this.order = order;
            }
        }

        /// <summary>A rule's palette built around the user's set colors.</summary>
        public struct Suggestion
        {
            public HarmonyRule rule;
            public ColorPalette palette;
            /// <summary>Set colors this suggestion had to replace to stay harmonious (empty = keeps them all).</summary>
            public List<PaletteSlot> replaced;
            /// <summary>Worst hue deviation (degrees) of a kept color from the rule's positions.</summary>
            public float error;
        }

        /// <summary>How far (degrees) a set color may sit from a rule's hue position and still count as fitting.</summary>
        const float FitTolerance = 20f;

        /// <summary>Hue positions of each rule relative to its base, on the RYB wheel.</summary>
        static float[] Positions(HarmonyRule rule)
        {
            switch (rule)
            {
                case HarmonyRule.Complementary: return new[] { 0f, 180f };
                case HarmonyRule.SplitComplementary: return new[] { 0f, 150f, 210f };
                case HarmonyRule.Analogous: return new[] { -30f, 0f, 30f, 60f };
                case HarmonyRule.Triadic: return new[] { 0f, 120f, 240f };
                case HarmonyRule.Tetradic: return new[] { 0f, 60f, 180f, 240f };
                default: return new[] { 0f };
            }
        }

        /// <summary>Grays, off-whites and near-blacks carry no hue, so they fit any rule.</summary>
        static bool IsChromatic(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            return s >= 0.15f && v >= 0.15f;
        }

        /// <summary>
        /// One suggestion per rule, built around <paramref name="setColors"/>. Rules that keep every set
        /// color come first (best fit first); a rule that can't fit them all replaces as few as possible,
        /// oldest first, preferring replacements close to the color they replace. The newest set color
        /// is always kept. <paramref name="fallbackBase"/> seeds the palette when nothing is set.
        /// </summary>
        public static List<Suggestion> Suggest(IReadOnlyList<SetColor> setColors, Color fallbackBase)
        {
            var result = new List<Suggestion>();
            foreach (var rule in AllRules)
                result.Add(SuggestFor(rule, setColors, fallbackBase));
            result.Sort((a, b) =>
            {
                int c = a.replaced.Count.CompareTo(b.replaced.Count);
                return c != 0 ? c : a.error.CompareTo(b.error);
            });
            return result;
        }

        static Suggestion SuggestFor(HarmonyRule rule, IReadOnlyList<SetColor> setColors, Color fallbackBase)
        {
            if (setColors.Count == 0)
                return new Suggestion { rule = rule, palette = Generate(fallbackBase, rule), replaced = new List<PaletteSlot>(), error = 0f };

            int newest = 0;
            for (int i = 1; i < setColors.Count; i++)
            {
                if (setColors[i].order > setColors[newest].order)
                    newest = i;
            }

            // Try every subset that keeps the newest color; few colors, so this is tiny (≤ 16 subsets).
            int n = setColors.Count, bestMask = -1;
            float bestBase = 0f, bestError = 0f;
            (int removed, int age, float similarity, float error) bestScore = (int.MaxValue, 0, 0f, 0f);
            for (int mask = 0; mask < (1 << n); mask++)
            {
                if ((mask & (1 << newest)) == 0)
                    continue;
                if (!Fit(rule, setColors, mask, out float baseHue, out float error))
                    continue;
                int removed = 0, age = 0;
                float similarity = 0f;
                var draft = Build(rule, setColors, mask, baseHue);
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        continue;
                    removed++;
                    age += setColors[i].order; // lower = older, preferred to drop
                    similarity += ColorDistance(setColors[i].color, draft.Get(setColors[i].slot));
                }
                var score = (removed, age, similarity, error);
                if (Better(score, bestScore))
                {
                    bestScore = score;
                    bestMask = mask;
                    bestBase = baseHue;
                    bestError = error;
                }
            }

            var replaced = new List<PaletteSlot>();
            for (int i = 0; i < n; i++)
            {
                if ((bestMask & (1 << i)) == 0)
                    replaced.Add(setColors[i].slot);
            }
            return new Suggestion { rule = rule, palette = Build(rule, setColors, bestMask, bestBase), replaced = replaced, error = bestError };
        }

        static bool Better((int removed, int age, float similarity, float error) a, (int removed, int age, float similarity, float error) b)
        {
            if (a.removed != b.removed) return a.removed < b.removed;
            if (a.age != b.age) return a.age < b.age;
            if (!Mathf.Approximately(a.similarity, b.similarity)) return a.similarity < b.similarity;
            return a.error < b.error;
        }

        /// <summary>Finds the base hue that best places the kept chromatic colors on the rule's positions.</summary>
        static bool Fit(HarmonyRule rule, IReadOnlyList<SetColor> setColors, int mask, out float baseHue, out float error)
        {
            var hues = new List<float>();
            for (int i = 0; i < setColors.Count; i++)
            {
                if ((mask & (1 << i)) != 0 && IsChromatic(setColors[i].color))
                {
                    ToRyb(setColors[i].color, out float h, out _, out _);
                    hues.Add(h);
                }
            }
            var positions = Positions(rule);
            baseHue = hues.Count > 0 ? hues[0] : 0f;
            error = 0f;
            if (hues.Count <= 1)
                return true; // one hue fits any rule's base position

            float best = float.MaxValue;
            for (float theta = 0f; theta < 360f; theta += 1f)
            {
                float worst = 0f;
                foreach (float h in hues)
                {
                    float nearest = float.MaxValue;
                    foreach (float p in positions)
                        nearest = Mathf.Min(nearest, Mathf.Abs(Mathf.DeltaAngle(h, theta + p)));
                    worst = Mathf.Max(worst, nearest);
                    if (worst >= best)
                        break;
                }
                if (worst < best)
                {
                    best = worst;
                    baseHue = theta;
                }
            }
            error = best;
            return best <= (rule == HarmonyRule.Monochromatic ? FitTolerance * 0.75f : FitTolerance);
        }

        /// <summary>Generates the rule's palette at <paramref name="baseHue"/> and pins the kept set colors into it.</summary>
        static ColorPalette Build(HarmonyRule rule, IReadOnlyList<SetColor> setColors, int mask, float baseHue)
        {
            // Saturation/brightness come from the dominant if it's set, else from the newest set color,
            // softened, because the generated dominant covers 60% of the room.
            Color reference = setColors[0].color;
            int newestOrder = int.MinValue;
            bool dominantSet = false;
            for (int i = 0; i < setColors.Count; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;
                if (setColors[i].slot == PaletteSlot.Dominant)
                {
                    reference = setColors[i].color;
                    dominantSet = true;
                }
                else if (!dominantSet && setColors[i].order > newestOrder)
                {
                    reference = setColors[i].color;
                    newestOrder = setColors[i].order;
                }
            }
            Color.RGBToHSV(reference, out _, out float s, out float v);
            if (!dominantSet)
            {
                s = Mathf.Min(s, 0.35f);
                v = Mathf.Max(v, 0.75f);
            }

            var palette = Generate(FromRyb(baseHue, s, v), rule);
            for (int i = 0; i < setColors.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                    palette.Set(setColors[i].slot, setColors[i].color);
            }
            return palette;
        }

        static float ColorDistance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        static float Wrap(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        static float Remap(float x, float[] from, float[] to)
        {
            for (int i = 0; i < from.Length - 1; i++)
            {
                if (x <= from[i + 1])
                    return Mathf.Lerp(to[i], to[i + 1], Mathf.InverseLerp(from[i], from[i + 1], x));
            }
            return to[to.Length - 1];
        }
    }
}
