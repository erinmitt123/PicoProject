using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// A design goal to work toward, e.g. "Cozy reading nook, warm palette, under 6 pieces". Each brief
    /// is scored live with up to three stars: ★ the must-have pieces are in the room, ★★ the palette
    /// fits the mood, ★★★ and it stays within the piece limit with nothing overlapping.
    /// </summary>
    public class DesignBrief
    {
        public readonly string id;
        public readonly string title;
        public readonly string pitch;
        public readonly (FurnitureKind kind, int count)[] mustHave;
        public readonly int maxPieces;
        public readonly string paletteGoal;
        readonly Func<ColorPalette, bool> m_PaletteFits;

        public DesignBrief(string id, string title, string pitch, (FurnitureKind, int)[] mustHave, int maxPieces,
            string paletteGoal, Func<ColorPalette, bool> paletteFits)
        {
            this.id = id;
            this.title = title;
            this.pitch = pitch;
            this.mustHave = mustHave;
            this.maxPieces = maxPieces;
            this.paletteGoal = paletteGoal;
            m_PaletteFits = paletteFits;
        }

        public struct Goal
        {
            public string text;
            public bool met;
        }

        public struct Score
        {
            public Goal[] goals;
            public int stars;
            public int pieces;
        }

        /// <summary>Scores the room as it stands. Throw pillows are accessories and don't count as pieces.</summary>
        public Score Evaluate(IReadOnlyList<FurnitureItem> items, ColorPalette palette, Func<FurnitureItem, bool> isWellPlaced)
        {
            var counts = new Dictionary<FurnitureKind, int>();
            int pieces = 0;
            bool allPlaced = true;
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                counts.TryGetValue(item.Kind, out int n);
                counts[item.Kind] = n + 1;
                if (item.Kind != FurnitureKind.ThrowPillow)
                    pieces++;
                if (isWellPlaced != null && !isWellPlaced(item))
                    allPlaced = false;
            }

            var goals = new List<Goal>();
            bool haveAll = true;
            foreach (var (kind, count) in mustHave)
            {
                counts.TryGetValue(kind, out int have);
                bool met = have >= count;
                haveAll &= met;
                string display = FurnitureCatalog.Get(kind).displayName;
                string text;
                if (Loc.IsEnglish)
                {
                    var name = display.ToLowerInvariant();
                    string article = "aeiou".IndexOf(name[0]) >= 0 ? "An" : "A";
                    text = count > 1 ? $"{count} × {name} ({Mathf.Min(have, count)}/{count})" : $"{article} {name}";
                }
                else
                {
                    text = count > 1 ? $"{count} × {Loc.T(display)} ({Mathf.Min(have, count)}/{count})" : Loc.F("A {0}", Loc.T(display));
                }
                goals.Add(new Goal { text = text, met = met });
            }
            bool paletteOk = palette != null && m_PaletteFits(palette);
            goals.Add(new Goal { text = Loc.T(paletteGoal), met = paletteOk });
            bool withinLimit = maxPieces <= 0 || pieces <= maxPieces;
            if (maxPieces > 0)
                goals.Add(new Goal { text = Loc.F("{0} pieces or fewer ({1} now)", maxPieces, pieces), met = withinLimit });
            goals.Add(new Goal { text = Loc.T("Nothing overlapping"), met = allPlaced });

            int stars = !haveAll ? 0 : !paletteOk ? 1 : withinLimit && allPlaced ? 3 : 2;
            return new Score { goals = goals.ToArray(), stars = stars, pieces = pieces };
        }

        // ---- palette moods ----------------------------------------------------------------------

        /// <summary>Most of the palette's colorful slots sit on the warm side of the wheel (reds, oranges, yellows).</summary>
        public static bool IsWarm(ColorPalette p)
        {
            int warm = 0, colorful = 0;
            foreach (var c in new[] { p.dominant, p.secondary, p.accent, p.highlight })
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (s < 0.12f || v < 0.12f)
                    continue;
                colorful++;
                float deg = h * 360f;
                if (deg < 70f || deg > 330f)
                    warm++;
            }
            return colorful > 0 && warm * 2 >= colorful + 1;
        }

        /// <summary>Soft and quiet: low average saturation, and no loud warm accents.</summary>
        public static bool IsCalm(ColorPalette p)
        {
            float sat = 0f;
            int loudWarm = 0;
            foreach (var c in new[] { p.dominant, p.secondary, p.accent })
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                sat += s;
                float deg = h * 360f;
                if (s > 0.55f && (deg < 50f || deg > 330f))
                    loudWarm++;
            }
            return sat / 3f < 0.42f && loudWarm == 0;
        }
    }

    /// <summary>The briefs, easiest first — each is a level.</summary>
    public static class BriefCatalog
    {
        public static readonly DesignBrief[] All =
        {
            new DesignBrief("reading-nook", "Cozy reading nook",
                "A snug corner to curl up with a book: somewhere to sit, light to read by, and books in reach.",
                new[] { (FurnitureKind.Armchair, 1), (FurnitureKind.FloorLamp, 1), (FurnitureKind.Bookshelf, 1) },
                5, "A warm palette (reds, oranges, yellows)", DesignBrief.IsWarm),

            new DesignBrief("calm-office", "Calm office for focus",
                "A quiet place to think: storage to keep the clutter away, a plant, and a comfortable chair.",
                new[] { (FurnitureKind.Bookshelf, 1), (FurnitureKind.Plant, 1), (FurnitureKind.Armchair, 1) },
                6, "A calm palette (soft, muted colors)", DesignBrief.IsCalm),

            new DesignBrief("conversation", "Conversation corner",
                "Seating that faces itself, so friends can talk across a table.",
                new[] { (FurnitureKind.Sofa, 1), (FurnitureKind.Armchair, 1), (FurnitureKind.CoffeeTable, 1), (FurnitureKind.Rug, 1) },
                8, "A complementary palette (opposites that pop)", p => p.harmony == HarmonyRule.Complementary || p.harmony == HarmonyRule.SplitComplementary),

            new DesignBrief("gallery", "Gallery wall",
                "Let the walls do the talking: three pieces of art, kept to one color family.",
                new[] { (FurnitureKind.WallArt, 3) },
                6, "A monochrome palette (one hue, many shades)", p => p.harmony == HarmonyRule.Monochromatic),

            new DesignBrief("green-retreat", "Green retreat",
                "Bring the outdoors in: three plants around a soft seat and a rug.",
                new[] { (FurnitureKind.Plant, 3), (FurnitureKind.Armchair, 1), (FurnitureKind.Rug, 1) },
                7, "An analogous palette (neighbors on the wheel)", p => p.harmony == HarmonyRule.Analogous),
        };

        public static DesignBrief Get(string id) => Array.Find(All, b => b.id == id);
    }
}
