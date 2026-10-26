using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The palette panel: filter chips (by color-wheel harmony, or just the user's own), a scrolling grid
    /// of palette cards with five swatches each, and buttons for the color wheel, shopping list, design
    /// briefs, lookbook and floor.
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

        public void Build(Camera eventCamera, PaletteManager palettes, FloorTapCalibrator calibrator)
        {
            m_Palettes = palettes;
            m_Calibrator = calibrator;

            const float scrollbar = 14f;
            float width = Pad * 2 + Columns * CardW + (Columns - 1) * Gap + scrollbar;
            var labels = new List<string>();
            foreach (var f in Filters)
                labels.Add(f.label);
            var chipRects = UIKit.FlowChips(labels, width - 2 * Pad, ChipH, 9f, out float chipsH);

            float chipsY = Pad + HeaderH;
            float gridY = chipsY + chipsH + Gap;
            float gridH = 3.5f * (CardH + Gap); // a partly visible fourth row shows there's more to scroll
            float buttonsY = gridY + gridH + Gap;
            float goalsY = buttonsY + ButtonH + Gap;
            float floorY = goalsY + ButtonH + Gap;
            float height = floorY + ButtonH + Pad;

            var canvas = UIKit.CreateWorldCanvas("Palette Dock Canvas", new Vector2(width, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 14f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var header = UIKit.Label(p, "Header", "Palettes", 14f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(header.rectTransform, Pad, Pad, width - 2 * Pad, HeaderH);

            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                var chip = UIKit.Button(p, $"Filter {Filters[i].label}", Filters[i].label, 9f, false, () => ApplyFilter(index));
                var r = chipRects[i];
                UIKit.Place((RectTransform)chip.transform, Pad + r.x, chipsY + r.y, r.width, r.height);
                m_Chips.Add(chip);
            }

            m_Grid = UIKit.ScrollGrid(p, "Palette Grid", Pad, gridY, width - 2 * Pad, gridH, new Vector2(CardW, CardH), Gap, Columns);
            m_Empty = UIKit.Label(p, "Empty", "No palettes of your own yet. Make one on the color wheel.", 10f, UIKit.MutedTextColor,
                TextAlignmentOptions.Center);
            UIKit.Place(m_Empty.rectTransform, Pad, gridY + 20f, width - 2 * Pad, 30f);

            foreach (var custom in m_Palettes.Customs)
                AddCard(custom, mine: true);
            foreach (var preset in PalettePresets.All)
                AddCard(preset, mine: false);

            float half = (width - 2 * Pad - Gap) / 2f;
            var wheel = UIKit.Button(p, "Color Wheel", "Color wheel", 10f, false, () => ColorWheelRequested?.Invoke());
            UIKit.Place((RectTransform)wheel.transform, Pad, buttonsY, half, ButtonH);
            var shop = UIKit.Button(p, "Shopping List", "Shopping list", 10f, false, () => ShoppingListRequested?.Invoke());
            UIKit.Place((RectTransform)shop.transform, Pad + half + Gap, buttonsY, half, ButtonH);
            var briefs = UIKit.Button(p, "Design Briefs", "Design briefs", 10f, false, () => BriefsRequested?.Invoke());
            UIKit.Place((RectTransform)briefs.transform, Pad, goalsY, half, ButtonH);
            var lookbook = UIKit.Button(p, "Lookbook", "Lookbook", 10f, false, () => LookbookRequested?.Invoke());
            UIKit.Place((RectTransform)lookbook.transform, Pad + half + Gap, goalsY, half, ButtonH);

            var floor = UIKit.Button(p, "Set Floor", "Set floor height", 10f, false, OnFloorPressed);
            UIKit.Place((RectTransform)floor.transform, Pad, floorY, width - 2 * Pad, ButtonH);
            m_FooterLabel = floor.GetComponentInChildren<TextMeshProUGUI>();

            m_Palettes.PaletteChanged += _ => RefreshSelection();
            m_Palettes.CustomPaletteChanged += OnCustomSaved;
            m_Calibrator.Completed += RefreshFooter;
            ApplyFilter(0);
            RefreshFooter();
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
            button.onClick.AddListener(() => m_Palettes.SetPalette(palette));
            card.gameObject.AddComponent<ForgivingClick>();

            var name = UIKit.Label(card.transform, "Name", palette.name, 9.5f, UIKit.TextColor, style: FontStyles.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 7.5f;
            name.fontSizeMax = 9.5f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(name.rectTransform, 6f, 3f, CardW - 16f, 13f);

            var rule = UIKit.Label(card.transform, "Harmony", (mine ? "Mine · " : "") + ColorHarmony.DisplayName(palette.harmony), 7.5f, UIKit.MutedTextColor);
            rule.textWrappingMode = TextWrappingModes.NoWrap;
            rule.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(rule.rectTransform, 6f, 16f, CardW - 16f, 10f);

            const float swatch = 15f, swatchGap = 2.5f;
            for (int i = 0; i < Slots.Length; i++)
            {
                var s = UIKit.Panel(card.transform, Slots[i].ToString(), palette.Get(Slots[i]), 4f);
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
            m_FooterLabel.text = m_Calibrator.IsActive
                ? "Touch or point at the floor · Cancel"
                : "Set floor height";
        }
    }
}
