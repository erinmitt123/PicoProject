using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The wider range of pieces, modelled in the same Scandinavian flat-pack style as the sofa and
    /// tables: rounded lacquered panels, linen and bouclé cushions, slim tapered oak legs. Each piece
    /// splits into parts that follow the palette (60-30-10 role or a fixed slot) and parts that keep
    /// their natural finish (oak, brass, glass). Roots sit on the floor and face +Z; wall pieces have
    /// their root at the bottom-centre of the back face.
    /// </summary>
    public partial class FurnitureFactory
    {
        Material m_MirrorGlass;

        /// <summary>A pale, glossy silver-blue for mirror glass.</summary>
        Material MirrorGlass
        {
            get
            {
                if (m_MirrorGlass == null)
                {
                    m_MirrorGlass = new Material(Paint) { name = "Mirror Glass" };
                    m_MirrorGlass.SetColor("_BaseColor", new Color(0.80f, 0.85f, 0.88f));
                    if (m_MirrorGlass.HasProperty("_Smoothness"))
                        m_MirrorGlass.SetFloat("_Smoothness", 0.95f);
                    if (m_MirrorGlass.HasProperty("_Metallic"))
                        m_MirrorGlass.SetFloat("_Metallic", 0.6f);
                }
                return m_MirrorGlass;
            }
        }

        bool BuildMore(Builder b, FurnitureKind kind, FurnitureVariant v)
        {
            switch (kind)
            {
                case FurnitureKind.Desk: Desk(b, v); return true;
                case FurnitureKind.DeskChair: Chair(b, v, desk: true); return true;
                case FurnitureKind.DiningTable: DiningTable(b, v); return true;
                case FurnitureKind.DiningChair: Chair(b, v, desk: false); return true;
                case FurnitureKind.Ottoman: Ottoman(b, v); return true;
                case FurnitureKind.Bench: Bench(b, v); return true;
                case FurnitureKind.ConsoleTable: ConsoleTable(b, v); return true;
                case FurnitureKind.Nightstand: Drawers(b, v, columns: 1, rows: Mathf.Max(1, v.seats)); return true;
                case FurnitureKind.Dresser: Drawers(b, v, columns: Mathf.Max(1, v.cells.x), rows: Mathf.Max(1, v.cells.y)); return true;
                case FurnitureKind.MediaConsole: MediaConsole(b, v); return true;
                case FurnitureKind.TableLamp: TableLamp(b, v); return true;
                case FurnitureKind.FloorMirror: FloorMirror(b, v); return true;
                case FurnitureKind.Vase: Vase(b, v); return true;
                case FurnitureKind.ThrowBlanket: ThrowBlanket(b, v); return true;
                case FurnitureKind.WallShelf: WallShelf(b, v); return true;
                default: return false;
            }
        }

        void Desk(Builder b, FurnitureVariant v)
        {
            // Writing desk: lacquered top over a slim drawer apron, a modesty panel at the back.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float top = 0.03f, apron = 0.09f, inset = 0.04f;
            b.Zone("Top", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint,
                Part.Box(new Vector3(0f, h - top / 2f, 0f), new Vector3(w, top, d), 0.01f));
            float apronW = w - 2f * inset - 0.06f;
            b.Zone("Drawer", ZoneColorSource.Slot, PaletteSlot.Accent, Paint,
                Part.Box(new Vector3(0f, h - top - apron / 2f, d / 2f - inset - 0.01f), new Vector3(apronW * 0.45f, apron, 0.02f), 0.006f));
            b.Zone("Frame", ZoneColorSource.Slot, PaletteSlot.Neutral, Paint,
                Part.Box(new Vector3(0f, h - top - apron / 2f, 0f), new Vector3(apronW, apron, d - 2f * inset - 0.04f), 0.006f),
                Part.Box(new Vector3(0f, h - top - 0.24f, -d / 2f + inset + 0.01f), new Vector3(apronW, 0.3f, 0.016f), 0.004f));
            b.Zone("Knob", ZoneColorSource.Fixed, PaletteSlot.Neutral, Metal,
                new Part(Sphere(), new Vector3(0f, h - top - apron / 2f, d / 2f - inset + 0.012f), Quaternion.identity, Vector3.one * 0.022f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - inset, d / 2f - inset, h - top, 0.024f, 0.016f));
        }

        void Chair(Builder b, FurnitureVariant v, bool desk)
        {
            // Dining chair: oak frame, a seat (wood or upholstered) and a curved back rail. The desk
            // chair adds a taller upholstered back and, optionally, arms.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            bool soft = desk || v.seats > 0;
            const float seatH = 0.46f;
            float seatT = soft ? 0.07f : 0.025f;
            b.Zone("Seat", ZoneColorSource.ItemRole, PaletteSlot.Accent, soft ? Upholstery : Paint,
                Part.Box(new Vector3(0f, seatH - seatT / 2f, 0.01f), new Vector3(w - 0.02f, seatT, d - 0.04f), soft ? 0.03f : 0.01f, soft ? 0.01f : 0f));
            float backH = desk ? 0.34f : 0.16f, backY = h - backH / 2f - (desk ? 0.02f : 0.04f);
            b.Zone("Back", desk ? ZoneColorSource.ItemRole : ZoneColorSource.Slot, desk ? PaletteSlot.Accent : PaletteSlot.Neutral, desk ? Upholstery : Paint,
                Part.Box(new Vector3(0f, backY, -d / 2f + 0.04f), new Vector3(w - 0.04f, backH, desk ? 0.06f : 0.025f), desk ? 0.03f : 0.01f, desk ? 0.008f : 0f,
                    Quaternion.Euler(-8f, 0f, 0f)));

            var frame = new List<Part>(Legs(w / 2f - 0.04f, d / 2f - 0.05f, seatH - seatT, 0.018f, 0.013f));
            // Back posts rise from the rear legs to the back rail.
            float postH = backY + backH / 2f - (seatH - seatT);
            foreach (float side in new[] { -1f, 1f })
                frame.Add(new Part(MeshKit.TaperedLeg(postH, 0.014f, 0.016f), new Vector3(side * (w / 2f - 0.04f), seatH - seatT, -d / 2f + 0.05f),
                    Quaternion.Euler(-6f, 0f, 0f)));
            if (desk && v.seats > 0)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    frame.Add(Part.Box(new Vector3(side * (w / 2f - 0.02f), seatH + 0.2f, -0.02f), new Vector3(0.035f, 0.025f, d * 0.7f), 0.01f));
                    frame.Add(Part.Box(new Vector3(side * (w / 2f - 0.02f), seatH + 0.1f, d * 0.28f), new Vector3(0.025f, 0.2f, 0.025f), 0.008f));
                }
            }
            b.Zone("Frame", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, frame.ToArray());
        }

        void DiningTable(Builder b, FurnitureVariant v)
        {
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float top = 0.035f;
            bool round = v.seats > 0;
            if (round)
            {
                // Round top on a turned pedestal with a disc foot.
                var disc = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(w / 2f, 0f), new Vector2(w / 2f, top), new Vector2(0f, top) }, 48);
                b.Zone("Top", ZoneColorSource.ItemRole, PaletteSlot.Dominant, Paint, new Part(disc, new Vector3(0f, h - top, 0f), Quaternion.identity));
                var pedestal = MeshKit.Lathe(new[] { new Vector2(0.24f, 0f), new Vector2(0.24f, 0.03f), new Vector2(0.06f, 0.06f),
                    new Vector2(0.045f, h * 0.5f), new Vector2(0.07f, h - top) }, 32);
                b.Zone("Pedestal", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, new Part(pedestal, Vector3.zero, Quaternion.identity));
                return;
            }
            b.Zone("Top", ZoneColorSource.ItemRole, PaletteSlot.Dominant, Paint,
                Part.Box(new Vector3(0f, h - top / 2f, 0f), new Vector3(w, top, d), 0.012f));
            b.Zone("Apron", ZoneColorSource.Slot, PaletteSlot.Neutral, Paint,
                Part.Box(new Vector3(0f, h - top - 0.04f, 0f), new Vector3(w - 0.16f, 0.08f, d - 0.16f), 0.006f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - 0.08f, d / 2f - 0.08f, h - top, 0.03f, 0.02f));
        }

        void Ottoman(Builder b, FurnitureVariant v)
        {
            float w = v.size.x, h = v.size.y, d = v.size.z;
            if (v.seats > 0)
            {
                // A round bouclé pouf: soft, slightly bulging sides, a piped seam at the top.
                var pouf = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(w * 0.44f, 0f), new Vector2(w * 0.5f, h * 0.45f),
                    new Vector2(w * 0.47f, h * 0.92f), new Vector2(w * 0.38f, h), new Vector2(0f, h) }, 40);
                b.Zone("Cover", ZoneColorSource.ItemRole, PaletteSlot.Accent, Rug, new Part(pouf, Vector3.zero, Quaternion.identity));
                var seam = MeshKit.Lathe(new[] { new Vector2(w * 0.47f, h * 0.9f), new Vector2(w * 0.475f, h * 0.93f) }, 40, capEnds: false);
                b.Zone("Piping", ZoneColorSource.Slot, PaletteSlot.Neutral, Upholstery, new Part(seam, Vector3.zero, Quaternion.identity), doubleSided: true);
                return;
            }
            // Upholstered block on short oak legs.
            const float legs = 0.08f;
            b.Zone("Cushion", ZoneColorSource.ItemRole, PaletteSlot.Accent, Upholstery,
                Part.Box(new Vector3(0f, legs + (h - legs) / 2f, 0f), new Vector3(w, h - legs, d), 0.05f, 0.012f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - 0.06f, d / 2f - 0.06f, legs, 0.018f, 0.014f));
        }

        void Bench(Builder b, FurnitureVariant v)
        {
            // Slatted oak bench with a loose seat pad.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float slat = 0.025f;
            var slats = new List<Part>();
            for (int i = 0; i < 4; i++)
            {
                float z = -d / 2f + d * (i + 0.5f) / 4f;
                slats.Add(Part.Box(new Vector3(0f, h - 0.04f - slat / 2f, z), new Vector3(w, slat, d / 4f - 0.012f), 0.006f));
            }
            slats.AddRange(Legs(w / 2f - 0.08f, d / 2f - 0.05f, h - 0.04f - slat, 0.022f, 0.016f));
            b.Zone("Frame", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, slats.ToArray());
            b.Zone("Pad", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Upholstery,
                Part.Box(new Vector3(0f, h - 0.02f, 0f), new Vector3(w - 0.06f, 0.04f, d - 0.03f), 0.018f, 0.006f));
        }

        void ConsoleTable(Builder b, FurnitureVariant v)
        {
            // Slim hallway console: lacquered top and low shelf on splayed oak legs.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float top = 0.03f;
            b.Zone("Top", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint,
                Part.Box(new Vector3(0f, h - top / 2f, 0f), new Vector3(w, top, d), 0.01f));
            b.Zone("Shelf", ZoneColorSource.Slot, PaletteSlot.Accent, Paint,
                Part.Box(new Vector3(0f, 0.2f, 0f), new Vector3(w - 0.1f, 0.02f, d - 0.06f), 0.006f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - 0.04f, d / 2f - 0.04f, h - top, 0.02f, 0.014f));
        }

        void Drawers(Builder b, FurnitureVariant v, int columns, int rows)
        {
            // Lacquered carcass with drawer fronts in an accent color, brass knobs, and short oak legs.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float legs = 0.12f, wall = 0.02f, gap = 0.006f;
            float bodyH = h - legs;
            b.Zone("Body", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint,
                Part.Box(new Vector3(0f, legs + bodyH / 2f, 0f), new Vector3(w, bodyH, d), 0.01f));
            var fronts = new List<Part>();
            var knobs = new List<Part>();
            float fw = (w - 2f * wall - (columns - 1) * gap) / columns;
            float fh = (bodyH - 2f * wall - (rows - 1) * gap) / rows;
            for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
            {
                float x = -w / 2f + wall + fw / 2f + c * (fw + gap);
                float y = legs + wall + fh / 2f + r * (fh + gap);
                fronts.Add(Part.Box(new Vector3(x, y, d / 2f + 0.006f), new Vector3(fw, fh, 0.016f), 0.005f));
                knobs.Add(new Part(Sphere(), new Vector3(x, y + fh * 0.15f, d / 2f + 0.022f), Quaternion.identity, Vector3.one * 0.024f));
            }
            b.Zone("Drawer Fronts", ZoneColorSource.Slot, PaletteSlot.Accent, Paint, fronts.ToArray());
            b.Zone("Knobs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Metal, knobs.ToArray());
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - 0.05f, d / 2f - 0.05f, legs, 0.02f, 0.015f));
        }

        void MediaConsole(Builder b, FurnitureVariant v)
        {
            // Low media unit: body, a row of slatted doors, an open cubby, legs.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            const float legs = 0.14f, wall = 0.02f, gap = 0.006f;
            int doors = Mathf.Max(2, v.cells.x);
            float bodyH = h - legs;
            b.Zone("Body", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint,
                Part.Box(new Vector3(0f, legs + bodyH / 2f, 0f), new Vector3(w, bodyH, d), 0.01f));
            var slats = new List<Part>();
            float dw = (w - 2f * wall - (doors - 1) * gap) / doors;
            for (int i = 0; i < doors; i++)
            {
                if (i == doors / 2)
                    continue; // the open cubby for a speaker or books
                float x = -w / 2f + wall + dw / 2f + i * (dw + gap);
                for (int s = 0; s < 7; s++)
                {
                    float sx = x - dw / 2f + dw * (s + 0.5f) / 7f;
                    slats.Add(Part.Box(new Vector3(sx, legs + bodyH / 2f, d / 2f + 0.008f), new Vector3(dw / 7f - 0.006f, bodyH - 2f * wall, 0.016f), 0.004f));
                }
            }
            b.Zone("Doors", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, slats.ToArray());
            float cubbyX = -w / 2f + wall + dw / 2f + (doors / 2) * (dw + gap);
            b.Zone("Cubby", ZoneColorSource.Slot, PaletteSlot.Accent, Paint,
                Part.Box(new Vector3(cubbyX, legs + bodyH / 2f, d / 2f - 0.15f), new Vector3(dw, bodyH - 2f * wall, 0.01f), 0.003f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(w / 2f - 0.06f, d / 2f - 0.06f, legs, 0.022f, 0.016f));
        }

        void TableLamp(Builder b, FurnitureVariant v)
        {
            // Ceramic gourd base, brass neck, linen drum shade.
            float w = v.size.x, h = v.size.y;
            float baseH = h * 0.5f, r = w * 0.38f;
            var basePart = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(r * 0.6f, 0f), new Vector2(r, baseH * 0.35f),
                new Vector2(r * 0.85f, baseH * 0.75f), new Vector2(r * 0.25f, baseH), new Vector2(0f, baseH) }, 32);
            b.Zone("Base", ZoneColorSource.ItemRole, PaletteSlot.Accent, Paint, new Part(basePart, Vector3.zero, Quaternion.identity));
            b.Zone("Neck", ZoneColorSource.Fixed, PaletteSlot.Neutral, Metal,
                new Part(MeshKit.Lathe(new[] { new Vector2(0.01f, 0f), new Vector2(0.01f, h * 0.16f) }, 12), new Vector3(0f, baseH, 0f), Quaternion.identity));
            var shade = MeshKit.Lathe(new[] { new Vector2(w / 2f, 0f), new Vector2(w * 0.4f, h * 0.36f) }, 32, capEnds: false);
            b.Zone("Shade", ZoneColorSource.Slot, PaletteSlot.Neutral, Upholstery, new Part(shade, new Vector3(0f, h * 0.62f, 0f), Quaternion.identity), doubleSided: true);
            b.Zone("Bulb", ZoneColorSource.Fixed, PaletteSlot.Neutral, Bulb, false,
                new Part(Sphere(), new Vector3(0f, h * 0.72f, 0f), Quaternion.identity, Vector3.one * w * 0.18f));
        }

        void FloorMirror(Builder b, FurnitureVariant v)
        {
            // Tall arched mirror in a slim frame on a low plinth, standing upright.
            float w = v.size.x, h = v.size.y;
            const float frame = 0.035f, depth = 0.035f, plinth = 0.04f;
            // Pale and glossy rather than dark metal: with nothing to reflect, a dark "mirror" reads as a chalkboard.
            b.Zone("Glass", ZoneColorSource.Fixed, PaletteSlot.Neutral, MirrorGlass, false,
                Part.Box(new Vector3(0f, plinth + h / 2f, 0f), new Vector3(w - 2f * frame, h - 2f * frame, 0.008f), 0.002f));
            b.Zone("Frame", ZoneColorSource.ItemRole, PaletteSlot.Accent, Paint,
                Part.Box(new Vector3(0f, plinth + frame / 2f, 0f), new Vector3(w, frame, depth), 0.008f),
                Part.Box(new Vector3(0f, plinth + h - frame / 2f, 0f), new Vector3(w, frame, depth), 0.008f),
                Part.Box(new Vector3(-(w - frame) / 2f, plinth + h / 2f, 0f), new Vector3(frame, h, depth), 0.008f),
                Part.Box(new Vector3((w - frame) / 2f, plinth + h / 2f, 0f), new Vector3(frame, h, depth), 0.008f));
            b.Zone("Plinth", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak,
                Part.Box(new Vector3(0f, plinth / 2f, 0f), new Vector3(w + 0.04f, plinth, 0.22f), 0.008f));
        }

        void Vase(Builder b, FurnitureVariant v)
        {
            // A wheel-thrown vase with a few dried stems.
            float w = v.size.x, h = v.size.y, r = w / 2f;
            var body = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(r * 0.7f, 0f), new Vector2(r, h * 0.35f),
                new Vector2(r * 0.55f, h * 0.8f), new Vector2(r * 0.4f, h * 0.92f), new Vector2(r * 0.48f, h), new Vector2(0f, h * 0.98f) }, 32);
            b.Zone("Glaze", ZoneColorSource.ItemRole, PaletteSlot.Accent, Paint, new Part(body, Vector3.zero, Quaternion.identity));
            var stems = new List<Part>();
            for (int i = 0; i < 3; i++)
            {
                float lean = (i - 1) * 9f;
                float len = h * (0.9f + 0.25f * (i % 2));
                stems.Add(new Part(MeshKit.TaperedLeg(len, 0.004f, 0.002f), new Vector3((i - 1) * r * 0.15f, h * 0.85f, 0f),
                    Quaternion.Euler(lean * 0.4f, 0f, lean)));
            }
            b.Zone("Stems", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, false, stems.ToArray());
        }

        void ThrowBlanket(Builder b, FurnitureVariant v)
        {
            // A folded knit throw: three soft layers with a contrasting stripe near the edge.
            float w = v.size.x, h = v.size.y, d = v.size.z;
            var layers = new List<Part>();
            for (int i = 0; i < 3; i++)
                layers.Add(Part.Box(new Vector3(0f, h * (i + 0.5f) / 3f, 0f), new Vector3(w - i * 0.01f, h / 3f, d - i * 0.008f), 0.014f, 0.006f));
            b.Zone("Knit", ZoneColorSource.ItemRole, PaletteSlot.Accent, Rug, layers.ToArray());
            b.Zone("Stripe", ZoneColorSource.Slot, PaletteSlot.Highlight, Rug,
                Part.Box(new Vector3(w * 0.32f, h * 0.5f, 0f), new Vector3(0.05f, h + 0.002f, d + 0.002f), 0.01f));
        }

        void WallShelf(Builder b, FurnitureVariant v)
        {
            // Floating oak shelf (or two) on brass brackets, styled with a few books and a small bowl.
            float w = v.size.x, t = v.size.y, d = v.size.z;
            int tiers = Mathf.Max(1, v.seats);
            const float spacing = 0.32f;
            var planks = new List<Part>();
            var brackets = new List<Part>();
            var books = new List<Part>();
            for (int i = 0; i < tiers; i++)
            {
                float y = 0.06f + i * spacing;
                planks.Add(Part.Box(new Vector3(0f, y + t / 2f, d / 2f), new Vector3(w, t, d), 0.006f));
                foreach (float side in new[] { -1f, 1f })
                    brackets.Add(Part.Box(new Vector3(side * (w / 2f - 0.1f), y - 0.03f, d * 0.35f), new Vector3(0.02f, 0.06f, d * 0.7f), 0.004f));
                float x = -w / 2f + 0.08f + (i % 2) * 0.3f;
                float[] heights = { 0.22f, 0.19f, 0.24f, 0.2f };
                for (int k = 0; k < 4; k++)
                {
                    float bh = heights[(k + i) % heights.Length];
                    books.Add(Part.Box(new Vector3(x, y + t + bh / 2f, d * 0.45f), new Vector3(0.028f, bh, 0.16f), 0.003f));
                    x += 0.033f;
                }
            }
            b.Zone("Shelf", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, planks.ToArray());
            b.Zone("Brackets", ZoneColorSource.Fixed, PaletteSlot.Neutral, Metal, brackets.ToArray());
            b.Zone("Books", ZoneColorSource.ItemRole, PaletteSlot.Accent, Paint, books.ToArray());
            var bowl = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.04f, 0f), new Vector2(0.07f, 0.05f), new Vector2(0.065f, 0.055f), new Vector2(0f, 0.01f) }, 24);
            b.Zone("Bowl", ZoneColorSource.Slot, PaletteSlot.Highlight, Paint,
                new Part(bowl, new Vector3(w / 2f - 0.16f, 0.06f + t, d / 2f), Quaternion.identity));
            b.AddCollider(new Vector3(0f, 0.06f + (tiers - 1) * spacing / 2f + 0.1f, d / 2f), new Vector3(w, (tiers - 1) * spacing + 0.3f, d));
        }
    }
}
