using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace PaletteRoom.EditorTools
{
    /// <summary>
    /// Gives the app its name and icon: "Palette Room", and a fan of five paint chips in the Scandinavian
    /// palette on a warm off-white tile, drawn in code at 1024 px and set as the default (all platforms,
    /// so the Android launcher uses it). Replace Assets/PaletteRoom/Icons/AppIcon.png with a designed icon
    /// any time and run this again to re-apply it without redrawing (it only draws when the file is missing
    /// or with Redraw).
    /// </summary>
    public static class AppIdentityBuilder
    {
        public const string ProductName = "Palette Room";
        const string IconPath = "Assets/PaletteRoom/Icons/AppIcon.png";
        const int Size = 1024;

        [MenuItem("Palette Room/Apply App Name and Icon")]
        public static void Apply() => Apply(redraw: false);

        [MenuItem("Palette Room/Redraw App Icon")]
        public static void Redraw() => Apply(redraw: true);

        static void Apply(bool redraw)
        {
            PlayerSettings.productName = ProductName;
            if (redraw || !File.Exists(IconPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, Draw().EncodeToPNG());
                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (importer != null && (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PaletteRoom] App name \"{ProductName}\" and icon {IconPath} applied.");
        }

        static readonly Color Background = Hex("#F4F1EB");
        static readonly Color[] Chips = { Hex("#3E5566"), Hex("#8A9FBF"), Hex("#DCD6CC"), Hex("#D98E4A"), Hex("#B5653F") };

        static Texture2D Draw()
        {
            const int ss = 3; // supersamples per axis
            var pixels = new Color[Size * Size];
            var pivot = new Vector2(Size * 0.5f, Size * 0.24f);
            float[] angles = { 36f, 18f, 0f, -18f, -36f }; // left to right
            var chipSize = new Vector2(Size * 0.16f, Size * 0.46f);
            float corner = Size * 0.04f, reach = Size * 0.29f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var sum = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                    // Warm tile, a touch lighter in the middle.
                    float r = Vector2.Distance(p, new Vector2(Size * 0.5f, Size * 0.55f)) / Size;
                    var c = Color.Lerp(Color.Lerp(Background, Color.white, 0.35f), Background * 0.97f, Mathf.Clamp01(r * 1.6f));
                    c.a = 1f;
                    for (int i = 0; i < 5; i++)
                    {
                        float a = angles[i] * Mathf.Deg2Rad;
                        var dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                        var centre = pivot + dir * reach;
                        var local = p - centre;
                        // Into the chip's frame (its long side along dir).
                        var q = new Vector2(Vector2.Dot(local, new Vector2(dir.y, -dir.x)), Vector2.Dot(local, dir));
                        // Soft shadow first, offset down-right.
                        float sd = RoundedBox(q - new Vector2(Size * 0.008f, -Size * 0.014f), chipSize / 2f, corner);
                        float shadow = Mathf.Clamp01(1f - (sd + Size * 0.01f) / (Size * 0.035f)) * 0.22f;
                        c = Color.Lerp(c, new Color(0.2f, 0.18f, 0.15f, 1f), shadow);
                        float d = RoundedBox(q, chipSize / 2f, corner);
                        if (d < 0f)
                        {
                            var chip = Chips[i];
                            // A paler label band at the chip's foot, like a paint card.
                            if (q.y < -chipSize.y / 2f + chipSize.y * 0.2f)
                                chip = Color.Lerp(chip, Color.white, 0.82f);
                            c = chip;
                        }
                    }
                    // The rivet that holds the fan together.
                    float pin = Vector2.Distance(p, pivot);
                    if (pin < Size * 0.035f)
                        c = pin < Size * 0.022f ? Hex("#2F3A44") : Color.white;
                    sum += c;
                }
                sum /= ss * ss;
                sum.a = 1f;
                pixels[y * Size + x] = sum;
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float RoundedBox(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
