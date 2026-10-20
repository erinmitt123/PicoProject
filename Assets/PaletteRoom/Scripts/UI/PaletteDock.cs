using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Floating panel of palette cards (five swatches each, labelled with their color-wheel harmony).
    /// Picking a card restyles the room. Also holds the user's own palette, the color wheel and
    /// shopping list buttons, and keeps the floor fallback reachable after the welcome panel is gone.
    /// </summary>
    public class PaletteDock : MonoBehaviour
    {
        const float CardW = 100f, CardH = 54f, Gap = 8f, Pad = 12f, HeaderH = 26f, ButtonH = 30f;
        const int Columns = 2;

        static readonly Color SelectedRing = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly PaletteSlot[] Slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));

        readonly List<Image> m_Rings = new List<Image>();
        PaletteManager m_Palettes;
        FloorTapCalibrator m_Calibrator;
        TextMeshProUGUI m_FooterLabel;

        Image m_CustomRing;
        TextMeshProUGUI m_CustomHint;
        readonly Image[] m_CustomSwatches = new Image[Slots.Length];

        public event Action ColorWheelRequested;
        public event Action ShoppingListRequested;

        public void Build(Camera eventCamera, PaletteManager palettes, FloorTapCalibrator calibrator)
        {
            m_Palettes = palettes;
            m_Calibrator = calibrator;

            var presets = PalettePresets.All;
            int rows = Mathf.CeilToInt(presets.Count / (float)Columns);
            float width = Pad * 2 + Columns * CardW + (Columns - 1) * Gap;
            float gridBottom = Pad + HeaderH + rows * CardH + (rows - 1) * Gap;
            float customY = gridBottom + Gap;
            float buttonsY = customY + CardH + Gap;
            float floorY = buttonsY + ButtonH + Gap;
            float height = floorY + ButtonH + Pad;

            var canvas = UIKit.CreateWorldCanvas("Palette Dock Canvas", new Vector2(width, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 14f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var header = UIKit.Label(p, "Header", "Palettes", 14f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(header.rectTransform, Pad, Pad, width - 2 * Pad, HeaderH);

            for (int i = 0; i < presets.Count; i++)
            {
                int col = i % Columns, row = i / Columns;
                float x = Pad + col * (CardW + Gap);
                float y = Pad + HeaderH + row * (CardH + Gap);
                var palette = presets[i];
                var card = BuildCard(p, palette.name, ColorHarmony.DisplayName(palette.harmony), x, y, CardW,
                    () => m_Palettes.SetPalette(palette), out var ring);
                m_Rings.Add(ring);
                AddSwatches(card.transform, 7f, 31f, palette, null);
            }

            BuildCustomCard(p, customY, width - 2 * Pad);

            float half = (width - 2 * Pad - Gap) / 2f;
            var wheel = UIKit.Button(p, "Color Wheel", "Color wheel", 10f, false, () => ColorWheelRequested?.Invoke());
            UIKit.Place((RectTransform)wheel.transform, Pad, buttonsY, half, ButtonH);
            var shop = UIKit.Button(p, "Shopping List", "Shopping list", 10f, false, () => ShoppingListRequested?.Invoke());
            UIKit.Place((RectTransform)shop.transform, Pad + half + Gap, buttonsY, half, ButtonH);

            var floor = UIKit.Button(p, "Set Floor", "Set floor height", 10f, false, OnFloorPressed);
            UIKit.Place((RectTransform)floor.transform, Pad, floorY, width - 2 * Pad, ButtonH);
            m_FooterLabel = floor.GetComponentInChildren<TextMeshProUGUI>();

            m_Palettes.PaletteChanged += _ => RefreshSelection();
            m_Palettes.CustomPaletteChanged += _ => RefreshCustom();
            m_Calibrator.Completed += RefreshFooter;
            RefreshCustom();
            RefreshSelection();
            RefreshFooter();
        }

        Image BuildCard(Transform parent, string title, string subtitle, float x, float y, float width, Action onClick, out Image ring)
        {
            // The ring sits behind the card and only shows on the active palette.
            ring = UIKit.Panel(parent, $"{title} Ring", SelectedRing, 10f);
            UIKit.Place(ring.rectTransform, x - 2f, y - 2f, width + 4f, CardH + 4f);
            ring.raycastTarget = false;

            var card = UIKit.Panel(parent, title, Color.white, 8f);
            UIKit.Place(card.rectTransform, x, y, width, CardH);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.colors = UIKit.SecondaryColors();
            button.onClick.AddListener(() => onClick());

            var name = UIKit.Label(card.transform, "Name", title, 9.5f, UIKit.TextColor, style: FontStyles.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 7.5f;
            name.fontSizeMax = 9.5f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(name.rectTransform, 7f, 5f, width - 14f, 13f);

            var rule = UIKit.Label(card.transform, "Harmony", subtitle, 7.5f, UIKit.MutedTextColor);
            rule.textWrappingMode = TextWrappingModes.NoWrap;
            rule.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(rule.rectTransform, 7f, 18f, width - 14f, 10f);
            return card;
        }

        static void AddSwatches(Transform card, float x, float y, ColorPalette palette, Image[] store)
        {
            const float swatch = 15f, swatchGap = 2.5f;
            for (int i = 0; i < Slots.Length; i++)
            {
                var s = UIKit.Panel(card, Slots[i].ToString(), palette != null ? palette.Get(Slots[i]) : Color.white, 4f);
                s.raycastTarget = false;
                UIKit.Place(s.rectTransform, x + i * (swatch + swatchGap), y, swatch, swatch);
                if (store != null)
                    store[i] = s;
            }
        }

        void BuildCustomCard(Transform parent, float y, float width)
        {
            // Before the user has made a palette, the card opens the color wheel instead of applying.
            var card = BuildCard(parent, "My palette", string.Empty, Pad, y, width, () =>
            {
                if (m_Palettes.Custom != null)
                    m_Palettes.SetPalette(m_Palettes.Custom);
                else
                    ColorWheelRequested?.Invoke();
            }, out m_CustomRing);
            m_CustomHint = card.transform.Find("Harmony").GetComponent<TextMeshProUGUI>();
            AddSwatches(card.transform, 7f, 31f, null, m_CustomSwatches);
        }

        void RefreshCustom()
        {
            var custom = m_Palettes.Custom;
            m_CustomHint.text = custom != null
                ? ColorHarmony.DisplayName(custom.harmony) + " · from your color wheel"
                : "Make your own on the color wheel";
            for (int i = 0; i < Slots.Length; i++)
            {
                m_CustomSwatches[i].enabled = custom != null;
                if (custom != null)
                    m_CustomSwatches[i].color = custom.Get(Slots[i]);
            }
        }

        void RefreshSelection()
        {
            var current = m_Palettes.Current;
            var presets = PalettePresets.All;
            for (int i = 0; i < m_Rings.Count; i++)
                m_Rings[i].enabled = presets[i] == current;
            m_CustomRing.enabled = current != null && current == m_Palettes.Custom;
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
                ? "Touch the floor and pinch · Cancel"
                : "Set floor height";
        }
    }
}
