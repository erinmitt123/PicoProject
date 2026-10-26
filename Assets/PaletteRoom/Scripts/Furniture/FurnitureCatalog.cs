using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// One size option of a catalog piece. Code-built pieces read <see cref="size"/> / <see cref="seats"/> /
    /// <see cref="cells"/>; scanned pieces are scaled by <see cref="scale"/> relative to their prefab.
    /// Sizes follow common retail dimensions (sofas, coffee tables, rugs) and ISO paper sizes (prints).
    /// </summary>
    public readonly struct FurnitureVariant
    {
        public readonly string id;
        /// <summary>Short name shown in lists, e.g. "Loveseat".</summary>
        public readonly string label;
        /// <summary>Human-readable size, e.g. "160 cm".</summary>
        public readonly string dimensions;
        /// <summary>Overall width × height × depth in metres (code-built pieces).</summary>
        public readonly Vector3 size;
        /// <summary>Per-axis scale applied to a scanned prefab.</summary>
        public readonly Vector3 scale;
        public readonly int seats;
        public readonly bool chaise;
        /// <summary>Cube-shelf columns × rows.</summary>
        public readonly Vector2Int cells;

        public FurnitureVariant(string id, string label, string dimensions, Vector3 size = default, Vector3? scale = null,
            int seats = 0, bool chaise = false, Vector2Int cells = default)
        {
            this.id = id;
            this.label = label;
            this.dimensions = dimensions;
            this.size = size;
            this.scale = scale ?? Vector3.one;
            this.seats = seats;
            this.chaise = chaise;
            this.cells = cells;
        }
    }

    /// <summary>Home-mode catalog: display name, 60-30-10 role and size variants for every placeable piece.</summary>
    public static class FurnitureCatalog
    {
        public readonly struct Entry
        {
            public readonly FurnitureKind kind;
            public readonly string displayName;
            /// <summary>Plural used by the shopping-list filter, e.g. "Sofas".</summary>
            public readonly string category;
            public readonly FurnitureRole role;
            public readonly FurnitureVariant[] variants;
            /// <summary>Index of the size used by the sample room.</summary>
            public readonly int defaultVariant;

            public Entry(FurnitureKind kind, string displayName, string category, FurnitureRole role, int defaultVariant, params FurnitureVariant[] variants)
            {
                this.kind = kind;
                this.displayName = displayName;
                this.category = category;
                this.role = role;
                this.defaultVariant = defaultVariant;
                this.variants = variants;
            }

            public FurnitureVariant Default => variants[defaultVariant];

            public FurnitureVariant Variant(string id)
            {
                foreach (var v in variants)
                {
                    if (v.id == id)
                        return v;
                }
                return Default;
            }
        }

        // Large surfaces carry the dominant color, supporting pieces the secondary, small pieces the accents.
        public static readonly IReadOnlyList<Entry> Home = new[]
        {
            new Entry(FurnitureKind.Sofa, "Sofa", "Sofas", FurnitureRole.Dominant, 1,
                new FurnitureVariant("loveseat", "Loveseat", "2-seat · 160 cm", new Vector3(1.6f, 0.9f, 0.88f), seats: 2),
                new FurnitureVariant("three", "3-seat", "215 cm", new Vector3(2.15f, 0.9f, 0.88f), seats: 3),
                new FurnitureVariant("chaise", "3-seat + chaise", "215 × 160 cm", new Vector3(2.15f, 0.9f, 1.6f), seats: 3, chaise: true)),
            new Entry(FurnitureKind.Rug, "Rug", "Rugs", FurnitureRole.Dominant, 1,
                new FurnitureVariant("4x6", "4 × 6 ft", "122 × 183 cm", new Vector3(1.83f, 0.014f, 1.22f)),
                new FurnitureVariant("5x8", "5 × 8 ft", "152 × 244 cm", new Vector3(2.44f, 0.014f, 1.52f)),
                new FurnitureVariant("8x10", "8 × 10 ft", "244 × 305 cm", new Vector3(3.05f, 0.014f, 2.44f))),
            new Entry(FurnitureKind.Armchair, "Armchair", "Armchairs", FurnitureRole.Secondary, 0,
                new FurnitureVariant("standard", "Lounge", "82 × 99 cm")),
            new Entry(FurnitureKind.CoffeeTable, "Coffee Table", "Coffee tables", FurnitureRole.Secondary, 1,
                new FurnitureVariant("small", "Small", "90 × 50 cm", new Vector3(0.9f, 0.42f, 0.5f)),
                new FurnitureVariant("medium", "Medium", "110 × 60 cm", new Vector3(1.1f, 0.43f, 0.6f)),
                new FurnitureVariant("large", "Large", "130 × 70 cm", new Vector3(1.3f, 0.43f, 0.7f))),
            new Entry(FurnitureKind.Bookshelf, "Bookshelf", "Bookshelves", FurnitureRole.Secondary, 1,
                new FurnitureVariant("2x2", "2 × 2", "77 × 77 cm", cells: new Vector2Int(2, 2)),
                new FurnitureVariant("2x3", "2 × 3", "77 × 112 cm", cells: new Vector2Int(2, 3)),
                new FurnitureVariant("2x4", "2 × 4 tall", "77 × 147 cm", cells: new Vector2Int(2, 4)),
                new FurnitureVariant("4x2", "4 × 2 wide", "147 × 77 cm", cells: new Vector2Int(4, 2))),
            new Entry(FurnitureKind.SideTable, "Side Table", "Side tables", FurnitureRole.Accent, 1,
                new FurnitureVariant("compact", "Compact", "40 × 40 × 50 cm", scale: new Vector3(0.727f, 0.909f, 0.889f)),
                new FurnitureVariant("standard", "Standard", "55 × 45 × 55 cm"),
                new FurnitureVariant("tall", "Tall", "55 × 45 × 65 cm", scale: new Vector3(1f, 1.18f, 1f))),
            new Entry(FurnitureKind.FloorLamp, "Floor Lamp", "Lamps", FurnitureRole.Accent, 0,
                new FurnitureVariant("tripod", "Tripod", "160 cm")),
            new Entry(FurnitureKind.Plant, "Plant", "Plants", FurnitureRole.Accent, 1,
                new FurnitureVariant("small", "Small", "55 cm", scale: Vector3.one * 0.65f),
                new FurnitureVariant("medium", "Medium", "84 cm"),
                new FurnitureVariant("large", "Floor plant", "115 cm", scale: Vector3.one * 1.37f)),
            new Entry(FurnitureKind.WallArt, "Wall Art", "Wall art", FurnitureRole.Accent, 1,
                new FurnitureVariant("a3", "A3", "30 × 42 cm", scale: Vector3.one * 0.5f),
                new FurnitureVariant("a2", "A2", "42 × 59 cm", scale: Vector3.one * 0.707f),
                new FurnitureVariant("a1", "A1", "59 × 84 cm")),
            new Entry(FurnitureKind.ThrowPillow, "Throw Pillow", "Pillows", FurnitureRole.Accent, 0,
                new FurnitureVariant("square", "Square", "50 × 50 cm"),
                new FurnitureVariant("large", "Large", "60 × 60 cm", scale: Vector3.one * 1.2f),
                new FurnitureVariant("lumbar", "Lumbar", "30 × 50 cm", scale: new Vector3(1f, 0.6f, 0.9f))),

            // ---- the wider range (built in FurnitureFactory.Pieces.cs) ----
            new Entry(FurnitureKind.Desk, "Desk", "Desks", FurnitureRole.Secondary, 1,
                new FurnitureVariant("compact", "Compact", "100 × 50 cm", new Vector3(1.0f, 0.75f, 0.5f)),
                new FurnitureVariant("standard", "Standard", "120 × 60 cm", new Vector3(1.2f, 0.75f, 0.6f)),
                new FurnitureVariant("large", "Large", "140 × 70 cm", new Vector3(1.4f, 0.75f, 0.7f))),
            new Entry(FurnitureKind.DeskChair, "Desk Chair", "Desk chairs", FurnitureRole.Accent, 0,
                new FurnitureVariant("standard", "Armless", "48 × 52 cm", new Vector3(0.48f, 0.84f, 0.52f)),
                new FurnitureVariant("arms", "With arms", "58 × 55 cm", new Vector3(0.58f, 0.86f, 0.55f), seats: 1)),
            new Entry(FurnitureKind.DiningTable, "Dining Table", "Dining tables", FurnitureRole.Dominant, 0,
                new FurnitureVariant("four", "4-seat", "140 × 80 cm", new Vector3(1.4f, 0.75f, 0.8f)),
                new FurnitureVariant("six", "6-seat", "180 × 90 cm", new Vector3(1.8f, 0.75f, 0.9f)),
                new FurnitureVariant("round", "Round", "Ø 110 cm", new Vector3(1.1f, 0.75f, 1.1f), seats: 1)),
            new Entry(FurnitureKind.DiningChair, "Dining Chair", "Dining chairs", FurnitureRole.Accent, 0,
                new FurnitureVariant("wood", "Wood seat", "45 × 50 cm", new Vector3(0.45f, 0.82f, 0.5f)),
                new FurnitureVariant("upholstered", "Upholstered", "48 × 54 cm", new Vector3(0.48f, 0.86f, 0.54f), seats: 1)),
            new Entry(FurnitureKind.Ottoman, "Ottoman", "Ottomans", FurnitureRole.Accent, 1,
                new FurnitureVariant("pouf", "Round pouf", "Ø 50 cm", new Vector3(0.5f, 0.42f, 0.5f), seats: 1),
                new FurnitureVariant("square", "Square", "60 × 60 cm", new Vector3(0.6f, 0.42f, 0.6f)),
                new FurnitureVariant("long", "Bench", "100 × 45 cm", new Vector3(1.0f, 0.42f, 0.45f))),
            new Entry(FurnitureKind.Bench, "Bench", "Benches", FurnitureRole.Secondary, 0,
                new FurnitureVariant("120", "120 cm", "120 × 38 cm", new Vector3(1.2f, 0.45f, 0.38f)),
                new FurnitureVariant("150", "150 cm", "150 × 38 cm", new Vector3(1.5f, 0.45f, 0.38f))),
            new Entry(FurnitureKind.ConsoleTable, "Console Table", "Console tables", FurnitureRole.Secondary, 0,
                new FurnitureVariant("100", "100 cm", "100 × 30 cm", new Vector3(1.0f, 0.78f, 0.3f)),
                new FurnitureVariant("120", "120 cm", "120 × 35 cm", new Vector3(1.2f, 0.78f, 0.35f))),
            new Entry(FurnitureKind.Nightstand, "Nightstand", "Nightstands", FurnitureRole.Accent, 0,
                new FurnitureVariant("one", "1 drawer", "45 × 40 cm", new Vector3(0.45f, 0.55f, 0.4f), seats: 1),
                new FurnitureVariant("two", "2 drawers", "45 × 40 cm", new Vector3(0.45f, 0.65f, 0.4f), seats: 2)),
            new Entry(FurnitureKind.Dresser, "Dresser", "Dressers", FurnitureRole.Secondary, 0,
                new FurnitureVariant("three", "3 drawers", "80 × 45 cm", new Vector3(0.8f, 0.85f, 0.45f), cells: new Vector2Int(1, 3)),
                new FurnitureVariant("six", "6 drawers", "140 × 45 cm", new Vector3(1.4f, 0.8f, 0.45f), cells: new Vector2Int(2, 3))),
            new Entry(FurnitureKind.MediaConsole, "Media Console", "Media consoles", FurnitureRole.Secondary, 0,
                new FurnitureVariant("150", "150 cm", "150 × 40 cm", new Vector3(1.5f, 0.5f, 0.4f), cells: new Vector2Int(3, 1)),
                new FurnitureVariant("180", "180 cm", "180 × 40 cm", new Vector3(1.8f, 0.5f, 0.4f), cells: new Vector2Int(4, 1))),
            new Entry(FurnitureKind.TableLamp, "Table Lamp", "Table lamps", FurnitureRole.Accent, 0,
                new FurnitureVariant("small", "Small", "40 cm", new Vector3(0.26f, 0.4f, 0.26f)),
                new FurnitureVariant("tall", "Tall", "60 cm", new Vector3(0.34f, 0.6f, 0.34f))),
            new Entry(FurnitureKind.FloorMirror, "Floor Mirror", "Mirrors", FurnitureRole.Accent, 0,
                new FurnitureVariant("standard", "Standard", "50 × 160 cm", new Vector3(0.5f, 1.6f, 0.04f)),
                new FurnitureVariant("wide", "Wide", "75 × 170 cm", new Vector3(0.75f, 1.7f, 0.04f))),
            new Entry(FurnitureKind.Vase, "Vase", "Vases", FurnitureRole.Accent, 1,
                new FurnitureVariant("bud", "Bud", "15 cm", new Vector3(0.09f, 0.15f, 0.09f)),
                new FurnitureVariant("medium", "Medium", "30 cm", new Vector3(0.16f, 0.3f, 0.16f)),
                new FurnitureVariant("floor", "Floor", "60 cm", new Vector3(0.28f, 0.6f, 0.28f))),
            new Entry(FurnitureKind.ThrowBlanket, "Throw Blanket", "Throws", FurnitureRole.Accent, 0,
                new FurnitureVariant("folded", "Folded", "130 × 170 cm", new Vector3(0.5f, 0.09f, 0.35f))),
            new Entry(FurnitureKind.WallShelf, "Wall Shelf", "Wall shelves", FurnitureRole.Accent, 0,
                new FurnitureVariant("single", "Single", "80 cm", new Vector3(0.8f, 0.03f, 0.22f), seats: 1),
                new FurnitureVariant("double", "Double", "80 cm, two tiers", new Vector3(0.8f, 0.03f, 0.22f), seats: 2)),
        };

        public static Entry Get(FurnitureKind kind)
        {
            foreach (var e in Home)
            {
                if (e.kind == kind)
                    return e;
            }
            throw new KeyNotFoundException($"No catalog entry for {kind}");
        }
    }
}
