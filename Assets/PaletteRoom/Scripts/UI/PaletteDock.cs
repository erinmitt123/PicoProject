using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The main panel, in three parts. In the middle, the palettes on a board shaped like a painter's
    /// palette: filter chips (by color-wheel harmony, or just the user's own), a scrolling grid of palette
    /// cards with five swatches each, and the color wheel. On the left, a rail with what the user does with
    /// the room (shopping list, lookbook, design briefs); on the right, a separate rail of set-up and app
    /// controls (floor height, tutorial, settings, exit).
    /// Palettes the user saves on the color wheel appear here alongside the presets. Picking a card
    /// restyles the room.
    /// </summary>
    public class PaletteDock : MonoBehaviour
    {
        const float Pad = 12f, HeaderH = 26f, CardW = 100f, CardH = 54f, Gap = 8f, ButtonH = 30f, ChipH = 22f;
        const int Columns = 3;
        const string All = "All", Mine = "Mine";

        static readonly Color SelectedRing = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly PaletteSlot[] Slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));

        // Filter chips, then which harmony each stands for (Mine/All are special).
        static readonly (string label, HarmonyRule? rule)[] Filters =
        {
            (All, null), (Mine, null),
            ("Complementary", HarmonyRule.Complementary), ("Split", HarmonyRule.SplitComplementary),
            ("Analogous", HarmonyRule.Analogous), ("Triadic", HarmonyRule.Triadic),
            ("Tetradic", HarmonyRule.Tetradic), ("Monochrome", HarmonyRule.Monochromatic),
        };

        class Card
        {
            public ColorPalette palette;
            public bool mine;
            public GameObject root;
            public Image ring;
        }

        readonly List<Card> m_Cards = new List<Card>();
        readonly List<Button> m_Chips = new List<Button>();
        PaletteManager m_Palettes;
        FloorTapCalibrator m_Calibrator;
        RectTransform m_Grid;
        TextMeshProUGUI m_Empty;
        TextMeshProUGUI m_FooterLabel;
        int m_Filter;

        public event Action ColorWheelRequested;
        public event Action ShoppingListRequested;
        public event Action BriefsRequested;
        public event Action LookbookRequested;
        public event Action TutorialRequested;
        public event Action SettingsRequested;
        public event Action ExitRequested;
        /// <summary>The user picked a palette card.</summary>
        public event Action<ColorPalette> PalettePicked;

        // The palette panel is shaped like a painter's palette: a left margin holds paint dabs in the
        // current colors and the thumb hole, the cards sit in the rest.
        const float Margin = 78f, RightPad = 18f, RailTile = 70f, ControlTile = 54f, RailPad = 12f, RailGap = 10f;
        const float RailYaw = 16f;   // side rails turn in toward the user, like a three-panel screen
        const float RailSpacing = 0.025f;
        static readonly Color PaletteWood = new Color(0.95f, 0.91f, 0.84f, 0.96f);

        readonly Image[] m_Dabs = new Image[5];

        public void Build(Camera eventCamera, PaletteManager palettes, FloorTapCalibrator calibrator)
        {
            m_Palettes = palettes;
            m_Calibrator = calibrator;

            const float scrollbar = 14f;
            float contentW = Columns * CardW + (Columns - 1) * Gap + scrollbar;
            float width = Margin + contentW + RightPad;
            var labels = new List<string>();
            foreach (var f in Filters)
                labels.Add(f.label);
            var chipRects = UIKit.FlowChips(labels, contentW, ChipH, 9f, out float chipsH);

            float top = Pad + 4f;
            float chipsY = top + HeaderH;
            float gridY = chipsY + chipsH + Gap;
            float gridH = 3.5f * (CardH + Gap); // a partly visible fourth row shows there's more to scroll
            float wheelY = gridY + gridH + Gap;
            float height = wheelY + ButtonH + Pad + 8f;

            // ---- the palette itself --------------------------------------------------------------
            var canvas = UIKit.CreateWorldCanvas("Palette Dock Canvas", new Vector2(width, height), transform, eventCamera);
            var board = BuildPaletteBoard(canvas.transform, width, height);
            var p = board.transform;

            var headerIcon = UIKit.Label(p, "Header Icon", Icons.Palette, 18f, UIKit.TextColor, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(headerIcon.rectTransform, Margin, top, 24f, HeaderH);
            var header = UIKit.Label(p, "Header", "Palettes", 14f, UIKit.TextColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(header.rectTransform, Margin + 26f, top, contentW - 26f, HeaderH);

            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                var chip = UIKit.Button(p, $"Filter {Filters[i].label}", Filters[i].label, 9f, false, () => ApplyFilter(index));
                var r = chipRects[i];
                UIKit.Place((RectTransform)chip.transform, Margin + r.x, chipsY + r.y, r.width, r.height);
                m_Chips.Add(chip);
            }

            m_Grid = UIKit.ScrollGrid(p, "Palette Grid", Margin, gridY, contentW, gridH, new Vector2(CardW, CardH), Gap, Columns);
            m_Empty = UIKit.Label(p, "Empty", "No palettes of your own yet. Make one on the color wheel.", 10f, UIKit.MutedTextColor,
                TextAlignmentOptions.Center);
            UIKit.Place(m_Empty.rectTransform, Margin, gridY + 20f, contentW, 30f);

            foreach (var custom in m_Palettes.Customs)
                AddCard(custom, mine: true);
            foreach (var preset in PalettePresets.All)
                AddCard(preset, mine: false);

            // Making a palette belongs with the palettes.
            var wheel = UIKit.Button(p, "Color Wheel", "Color wheel", 10.5f, true, () => ColorWheelRequested?.Invoke(), Icons.ColorWheel);
            UIKit.Place((RectTransform)wheel.transform, Margin, wheelY, contentW, ButtonH);

            // ---- left rail: what you do with the room ----------------------------------------------
            var activities = new (string name, string caption, string icon, Action action)[]
            {
                ("Shopping List", "Shopping list", Icons.Shopping, () => ShoppingListRequested?.Invoke()),
                ("Lookbook", "Lookbook", Icons.Lookbook, () => LookbookRequested?.Invoke()),
                ("Design Briefs", "Design briefs", Icons.Briefs, () => BriefsRequested?.Invoke()),
            };
            BuildRail("Activities Canvas", eventCamera, activities, RailTile, RailGap, -1f, width);

            // ---- right rail: set-up and app controls, kept apart from the room's tools --------------
            var controls = new (string name, string caption, string icon, Action action)[]
            {
                ("Set Floor", "Floor height", Icons.Floor, OnFloorPressed),
                ("Tutorial", "Tutorial", Icons.Tutorial, () => TutorialRequested?.Invoke()),
                ("Settings", "Settings", Icons.Settings, () => SettingsRequested?.Invoke()),
                ("Exit", "Exit", Icons.Exit, () => ExitRequested?.Invoke()),
            };
            var controlRail = BuildRail("Controls Canvas", eventCamera, controls, ControlTile, 8f, 1f, width);
            m_FooterLabel = controlRail.Find("Set Floor/Label").GetComponent<TextMeshProUGUI>();

            m_Palettes.PaletteChanged += _ => RefreshSelection();
            m_Palettes.CustomPaletteChanged += OnCustomSaved;
            m_Calibrator.Completed += RefreshFooter;
            ApplyFilter(0);
            RefreshFooter();
        }

        /// <summary>
        /// The painter's palette board: a soft wooden shape with a thumb hole and a notch on the left, and
        /// five dabs of paint in the current palette's colors above the hole.
        /// </summary>
        Image BuildPaletteBoard(Transform canvas, float width, float height)
        {
            var shadow = new GameObject("Shadow", typeof(RectTransform)).AddComponent<Image>();
            shadow.transform.SetParent(canvas, false);
            shadow.sprite = PaletteSprite(width, height, shadowPass: true);
            shadow.color = new Color(0f, 0f, 0f, 0.22f);
            shadow.raycastTarget = false;
            UIKit.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-10f, -18f);
            shadow.rectTransform.offsetMax = new Vector2(10f, 4f);

            var board = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
            board.transform.SetParent(canvas, false);
            board.sprite = PaletteSprite(width, height, shadowPass: false);
            board.color = PaletteWood;
            UIKit.Stretch(board.rectTransform);

            const float dab = 30f;
            float[] wobble = { 0f, 5f, -3f, 4f, -2f };
            for (int i = 0; i < m_Dabs.Length; i++)
            {
                float size = dab + (i % 2 == 0 ? 2f : -2f);
                m_Dabs[i] = UIKit.Edge(UIKit.Panel(board.transform, $"Paint {i + 1}", Color.white, size / 2f));
                m_Dabs[i].raycastTarget = false;
                UIKit.Place(m_Dabs[i].rectTransform, Margin / 2f - size / 2f + wobble[i], 28f + i * 44f, size, size);
            }
            RefreshDabs();
            m_Palettes.PaletteChanged += _ => RefreshDabs();

            var overlay = canvas.GetComponent<OverlayCanvas>();
            if (overlay != null)
                overlay.IsWindow = true;
            return board;
        }

        void RefreshDabs()
        {
            var palette = m_Palettes.Current;
            for (int i = 0; i < m_Dabs.Length; i++)
                m_Dabs[i].color = palette.Get(Slots[i]);
        }

        // Thumb hole and notch, in mm from the board's top-left.
        static Vector2 HoleCentre(float height) => new Vector2(Margin / 2f, height - 96f);
        const float HoleRadius = 17f;

        /// <summary>The board's outline as a mipmapped sprite (or a blurred copy for its shadow).</summary>
        static Sprite PaletteSprite(float width, float height, bool shadowPass)
        {
            const int size = 1024;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = shadowPass ? "Palette Board Shadow" : "Palette Board",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var hole = HoleCentre(height);
            var notch = new Vector2(-8f, height - 44f);
            float mmPerPx = Mathf.Max(width, height) / size;
            float soft = shadowPass ? 12f : mmPerPx; // the shadow fades over 12 mm, the board over a pixel
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Texture y runs up; the layout's runs down.
                var mm = new Vector2((x + 0.5f) / size * width, (1f - (y + 0.5f) / size) * height);
                float d = RoundedBox(mm - new Vector2(width, height) / 2f, new Vector2(width, height) / 2f - Vector2.one * soft, 70f);
                if (!shadowPass)
                {
                    d = Mathf.Max(d, HoleRadius - Vector2.Distance(mm, hole));
                    d = Mathf.Max(d, 34f - Vector2.Distance(mm, notch));
                }
                float a = Mathf.Clamp01(0.5f - d / soft);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static float RoundedBox(Vector2 p, Vector2 half, float r)
        {
            r = Mathf.Min(r, Mathf.Min(half.x, half.y));
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>
        /// A column of icon tiles in its own small panel beside the palette (<paramref name="side"/> −1 left,
        /// +1 right), turned in toward the user.
        /// </summary>
        Transform BuildRail(string name, Camera eventCamera, (string name, string caption, string icon, Action action)[] tiles,
            float tile, float gap, float side, float paletteWidth)
        {
            float w = tile + 2 * RailPad;
            float h = tiles.Length * tile + (tiles.Length - 1) * gap + 2 * RailPad;
            var canvas = UIKit.CreateWorldCanvas(name, new Vector2(w, h), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 14f);
            UIKit.Stretch(panel.rectTransform);
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                var action = t.action;
                var button = UIKit.IconTile(panel.transform, t.name, t.icon, t.caption, tile, () => action());
                UIKit.Place((RectTransform)button.transform, RailPad, RailPad + i * (tile + gap), tile, tile);
            }
            // Hinged at the palette's edge, then swung in toward the user.
            float mm = UIKit.MillimetresToMetres;
            var hinge = new Vector3(side * (paletteWidth / 2f * mm + RailSpacing), 0f, 0f);
            var rotation = Quaternion.Euler(0f, side * RailYaw, 0f);
            canvas.transform.localRotation = rotation;
            canvas.transform.localPosition = hinge + rotation * new Vector3(side * w / 2f * mm, 0f, 0f);
            return panel.transform;
        }

        void OnCustomSaved(ColorPalette palette)
        {
            // Newest of the user's palettes goes first in the grid.
            var card = AddCard(palette, mine: true);
            card.root.transform.SetSiblingIndex(0);
            ApplyFilter(m_Filter);
        }

        Card AddCard(ColorPalette palette, bool mine)
        {
            // A cell holds the selection ring behind an inset card, so the grid sees one item per palette.
            var cell = new GameObject(palette.name, typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(m_Grid, false);

            var ring = UIKit.Panel(cell, "Ring", SelectedRing, 10f);
            ring.raycastTarget = false;
            UIKit.Stretch(ring.rectTransform);

            var card = UIKit.Panel(cell, "Card", Color.white, 8f);
            UIKit.Stretch(card.rectTransform, 2f);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.colors = UIKit.SecondaryColors();
            button.onClick.AddListener(() =>
            {
                m_Palettes.SetPalette(palette);
                PalettePicked?.Invoke(palette);
            });
            card.gameObject.AddComponent<ForgivingClick>();

            var name = UIKit.Label(card.transform, "Name", palette.name, 9.5f, UIKit.TextColor, style: FontStyles.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 7.5f;
            name.fontSizeMax = 9.5f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(name.rectTransform, 6f, 3f, CardW - 16f, 13f);

            var rule = UIKit.Label(card.transform, "Harmony", string.Empty, 7.5f, UIKit.MutedTextColor);
            rule.Localize(mine ? "Mine · {0}" : "{0}", Loc.Arg(ColorHarmony.DisplayName(palette.harmony)));
            rule.textWrappingMode = TextWrappingModes.NoWrap;
            rule.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(rule.rectTransform, 6f, 16f, CardW - 16f, 10f);

            const float swatch = 15f, swatchGap = 2.5f;
            for (int i = 0; i < Slots.Length; i++)
            {
                var s = UIKit.Edge(UIKit.Panel(card.transform, Slots[i].ToString(), palette.Get(Slots[i]), 4f));
                s.raycastTarget = false;
                UIKit.Place(s.rectTransform, 6f + i * (swatch + swatchGap), 29f, swatch, swatch);
            }

            var entry = new Card { palette = palette, mine = mine, root = cell.gameObject, ring = ring };
            m_Cards.Add(entry);
            return entry;
        }

        void ApplyFilter(int index)
        {
            m_Filter = index;
            var (label, rule) = Filters[index];
            int shown = 0;
            foreach (var card in m_Cards)
            {
                bool show = label == All
                    || (label == Mine && card.mine)
                    || (rule.HasValue && card.palette.harmony == rule.Value);
                card.root.SetActive(show);
                if (show)
                    shown++;
            }
            m_Empty.gameObject.SetActive(label == Mine && shown == 0);
            for (int i = 0; i < m_Chips.Count; i++)
                UIKit.SetChipSelected(m_Chips[i], i == index);
            RefreshSelection();
        }

        void RefreshSelection()
        {
            var current = m_Palettes.Current;
            foreach (var card in m_Cards)
                card.ring.enabled = card.palette == current;
        }

        void OnFloorPressed()
        {
            if (m_Calibrator.IsActive)
                m_Calibrator.Cancel();
            else
                m_Calibrator.Begin();
            RefreshFooter();
        }

        void RefreshFooter()
        {
            m_FooterLabel.Localize(m_Calibrator.IsActive
                ? "Cancel"
                : "Floor height");
        }
    }
}
