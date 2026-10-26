using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Opens next to a clicked piece and lets the user choose each part's color: "Auto" follows the
    /// 60-30-10 rule, or pin the part to any of the current palette's five colors. Pinned parts keep
    /// their slot when the palette changes, so a piece stays coordinated while still being the user's.
    /// </summary>
    public class ObjectColorPanel : MonoBehaviour
    {
        const float W = 430f, Pad = 22f, RowH = 34f, RowGap = 8f, NameW = 104f, AutoW = 58f, Swatch = 36f, SwatchGap = 5f;

        static readonly Color Ring = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly PaletteSlot[] Slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));

        class Row
        {
            public PaintableZone zone;
            public Image autoRing;
            public Image[] swatches;
            public Image[] rings;
        }

        PaletteManager m_Palettes;
        Transform m_Head;
        Camera m_Camera;
        FurnitureItem m_Item;
        RectTransform m_CanvasRect;
        Transform m_Body;
        TextMeshProUGUI m_Title, m_Subtitle;
        readonly List<Row> m_Rows = new List<Row>();

        public void Build(Camera eventCamera, PaletteManager palettes)
        {
            m_Camera = eventCamera;
            m_Head = eventCamera.transform;
            m_Palettes = palettes;
            var canvas = UIKit.CreateWorldCanvas("Object Color Canvas", new Vector2(W, 200f), transform, eventCamera);
            m_CanvasRect = (RectTransform)canvas.transform;
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 18f);
            UIKit.Stretch(panel.rectTransform);
            m_Body = panel.transform;

            m_Title = UIKit.Label(m_Body, "Title", string.Empty, 18f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad, W - 2 * Pad, 24f);
            m_Subtitle = UIKit.Label(m_Body, "Subtitle", "Pick a color for each part. Auto follows the palette's 60-30-10 balance.", 10f, UIKit.MutedTextColor);
            UIKit.Place(m_Subtitle.rectTransform, Pad, Pad + 26f, W - 2 * Pad, 14f);

            m_Palettes.PaletteChanged += _ => Refresh();
        }

        /// <summary>Shows the parts of <paramref name="item"/> and places the panel beside it, facing the user.</summary>
        public void Open(FurnitureItem item)
        {
            m_Item = item;
            var entry = FurnitureCatalog.Get(item.Kind);
            m_Title.text = $"{entry.displayName} · {entry.Variant(item.VariantId).label}";
            RebuildRows();
            gameObject.SetActive(true);
            PlaceBeside(item);
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        void RebuildRows()
        {
            foreach (var row in m_Rows)
                Destroy(row.autoRing.transform.parent.gameObject);
            m_Rows.Clear();
            foreach (Transform child in m_Body)
            {
                if (child.name == "Footer")
                    Destroy(child.gameObject);
            }

            float y = Pad + 50f;
            foreach (var zone in m_Item.Zones)
            {
                if (!zone.AllowOverride)
                    continue;
                BuildRow(zone, y);
                y += RowH + RowGap;
            }

            var footer = new GameObject("Footer", typeof(RectTransform)).GetComponent<RectTransform>();
            footer.SetParent(m_Body, false);
            UIKit.Place(footer, Pad, y + 4f, W - 2 * Pad, 34f);
            float half = (W - 2 * Pad - 10f) / 2f;
            var reset = UIKit.Button(footer, "Reset", "Reset to palette", 11f, false, ResetAll);
            UIKit.Place((RectTransform)reset.transform, 0f, 0f, half, 34f);
            var close = UIKit.Button(footer, "Close", "Done", 12f, true, Close);
            UIKit.Place((RectTransform)close.transform, half + 10f, 0f, half, 34f);

            m_CanvasRect.sizeDelta = new Vector2(W, y + 4f + 34f + Pad);
            // The panel's height depends on the piece, so the grab bar follows the new bottom edge.
            var movable = GetComponent<MovablePanel>();
            if (movable != null)
                movable.Relayout();
        }

        void BuildRow(PaintableZone zone, float y)
        {
            var rowRoot = new GameObject(zone.ZoneName, typeof(RectTransform)).GetComponent<RectTransform>();
            rowRoot.SetParent(m_Body, false);
            UIKit.Place(rowRoot, Pad, y, W - 2 * Pad, RowH);

            var name = UIKit.Label(rowRoot, "Name", zone.ZoneName, 12f, UIKit.TextColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(name.rectTransform, 0f, 0f, NameW, RowH);

            var row = new Row { zone = zone, swatches = new Image[Slots.Length], rings = new Image[Slots.Length] };
            float x = NameW;
            row.autoRing = Option(rowRoot, "Auto", x, AutoW, null, () => Pick(zone, null), out _);
            x += AutoW + SwatchGap;
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                row.rings[i] = Option(rowRoot, slot.ToString(), x, Swatch, slot, () => Pick(zone, slot), out row.swatches[i]);
                x += Swatch + SwatchGap;
            }
            m_Rows.Add(row);
        }

        /// <summary>One choice: a ring (shown when selected) behind either a swatch or the "Auto" chip.</summary>
        Image Option(Transform parent, string name, float x, float width, PaletteSlot? slot, Action onClick, out Image swatch)
        {
            var ring = UIKit.Panel(parent, $"{name} Ring", Ring, 9f);
            ring.raycastTarget = false;
            UIKit.Place(ring.rectTransform, x - 2f, -2f, width + 4f, RowH + 4f);

            var face = UIKit.Panel(parent, name, slot.HasValue ? Color.white : UIKit.SecondaryColor, 7f);
            UIKit.Place(face.rectTransform, x, 0f, width, RowH);
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = slot.HasValue ? Selectable.Transition.None : Selectable.Transition.ColorTint;
            if (!slot.HasValue)
            {
                button.colors = UIKit.SecondaryColors();
                var label = UIKit.Label(face.transform, "Label", "Auto", 10.5f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(label.rectTransform);
            }
            button.onClick.AddListener(() => onClick());
            face.gameObject.AddComponent<ForgivingClick>();
            swatch = face;
            return ring;
        }

        void Pick(PaintableZone zone, PaletteSlot? slot)
        {
            zone.SetOverride(slot, PaletteManager.BlendDuration);
            m_Palettes.RefreshItem(m_Item);
            Refresh();
        }

        void ResetAll()
        {
            foreach (var row in m_Rows)
                row.zone.SetOverride(null, PaletteManager.BlendDuration);
            m_Palettes.RefreshItem(m_Item);
            Refresh();
        }

        void Refresh()
        {
            if (m_Item == null)
                return;
            var palette = m_Palettes.Current;
            foreach (var row in m_Rows)
            {
                var pinned = row.zone.Override;
                row.autoRing.enabled = !pinned.HasValue;
                for (int i = 0; i < Slots.Length; i++)
                {
                    row.swatches[i].color = palette.Get(Slots[i]);
                    row.rings[i].enabled = pinned.HasValue && pinned.Value == Slots[i];
                }
            }
        }

        void PlaceBeside(FurnitureItem item)
        {
            // Above the piece, pulled a third of the way toward the user, at a comfortable distance.
            var box = item.GetComponent<BoxCollider>();
            var top = item.transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f);
            var target = Vector3.Lerp(top + Vector3.up * 0.3f, m_Head.position, 0.35f);
            target.y = Mathf.Clamp(target.y, m_Head.position.y - 0.45f, m_Head.position.y + 0.1f);
            var toTarget = target - m_Head.position;
            float d = Mathf.Clamp(toTarget.magnitude, 0.6f, 1.3f);
            target = m_Head.position + toTarget.normalized * d;
            transform.position = target;
            var facing = Vector3.ProjectOnPlane(target - m_Head.position, Vector3.up);
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        }
    }
}
