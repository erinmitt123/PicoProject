using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// A saved room as a shopping list to take to the store: the palette, then every piece with its
    /// size, dimensions and the color of each part (in words and hex), with identical pieces grouped.
    /// </summary>
    public static class RoomList
    {
        public static string Subject(Lookbook.Entry entry) => $"Palette Room: {entry.title} ({entry.date})";

        public static string Build(Lookbook.Entry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{entry.title} — designed in Palette Room, {entry.date}");
            if (!string.IsNullOrEmpty(entry.briefId))
                sb.AppendLine($"Design brief: {entry.stars} of 3 stars");
            sb.AppendLine();

            sb.AppendLine($"PALETTE: {entry.paletteName}");
            string[] roles = { "Main (60%)", "Supporting (30%)", "Accent (10%)", "Neutral", "Highlight" };
            if (entry.paletteHex != null)
                for (int i = 0; i < entry.paletteHex.Length && i < roles.Length; i++)
                    sb.AppendLine($"  {roles[i]}: {ColorNames.Describe(entry.paletteHex[i])} {entry.paletteHex[i]}");
            sb.AppendLine();

            // Group identical pieces (same kind, size and colors) so the list reads like a shopping list.
            var groups = new List<(string key, Lookbook.Piece piece, int count)>();
            foreach (var piece in entry.pieces)
            {
                string key = piece.kind + "|" + piece.variant + "|" + ColorsKey(piece);
                int i = groups.FindIndex(g => g.key == key);
                if (i >= 0)
                    groups[i] = (key, groups[i].piece, groups[i].count + 1);
                else
                    groups.Add((key, piece, 1));
            }

            sb.AppendLine($"PIECES ({entry.pieces.Count})");
            int n = 1;
            foreach (var (_, piece, count) in groups)
            {
                string name = piece.kind, size = piece.variant, dims = "";
                if (Enum.TryParse(piece.kind, out FurnitureKind kind))
                {
                    var catalog = FurnitureCatalog.Get(kind);
                    var variant = catalog.Variant(piece.variant);
                    name = catalog.displayName;
                    size = variant.label;
                    dims = variant.dimensions;
                }
                sb.Append($"{n++}. {name}{(count > 1 ? $" × {count}" : "")} — {size}");
                if (!string.IsNullOrEmpty(dims) && dims != size)
                    sb.Append($", {dims}");
                sb.AppendLine();
                foreach (var part in piece.colors)
                {
                    sb.AppendLine(part.natural
                        ? $"     {part.part}: natural finish"
                        : $"     {part.part}: {ColorNames.Describe(part.hex)} {part.hex}");
                }
            }
            return sb.ToString();
        }

        static string ColorsKey(Lookbook.Piece piece)
        {
            var sb = new StringBuilder();
            foreach (var c in piece.colors)
                sb.Append(c.part).Append('=').Append(c.natural ? "natural" : c.hex).Append(';');
            return sb.ToString();
        }
    }
}
