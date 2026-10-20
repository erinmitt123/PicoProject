using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The home catalog as a filterable, paged grid: one tile per size of each piece, with a baked
    /// picture, its size, its 60-30-10 role color, how many are in the room, and add/remove buttons.
    /// Pictures are flat sprites, so a tile costs a quad and a small texture.
    /// </summary>
    public class ShoppingListPanel : MonoBehaviour
    {
        const float Pad = 24f, TileW = 96f, TileH = 156f, Gap = 10f, FooterH = 44f, Icon = 70f, ChipH = 24f;
        const int Columns = 5, Rows = 2, PerPage = Columns * Rows;
        const string AllCategory = "All";

        [SerializeField] float distance = 0.8f;
        [SerializeField] float belowEyes = 0.15f;

        struct Option
        {
            public FurnitureCatalog.Entry entry;
            public FurnitureVariant variant;
        }

        class Tile
        {
            public GameObject root;
            public Image icon, dot;
            public TextMeshProUGUI name, size, dims, count;
            public Button remove;
        }

        class Chip
        {
            public string category;
            public Button button;
            public TextMeshProUGUI label;
        }

        readonly List<Tile> m_Tiles = new List<Tile>();
        readonly List<Chip> m_Chips = new List<Chip>();
        readonly List<Option> m_Visible = new List<Option>();
        PaletteManager m_Palettes;
        RoomFurniture m_Room;
        Transform m_Head;
        string m_Category = AllCategory;
        int m_Page;
        TextMeshProUGUI m_PageLabel;
        Button m_Prev, m_Next;

        public event Action<FurnitureKind, string> AddRequested;
        public event Action Closed;

        public void Build(Camera eventCamera, PaletteManager palettes, RoomFurniture room)
        {
            m_Palettes = palettes;
            m_Room = room;
            m_Head = eventCamera.transform;

            float width = 2 * Pad + Columns * TileW + (Columns - 1) * Gap;

            // Lay the chips out first so the grid can start below however many rows they need.
            var categories = new List<string> { AllCategory };
            foreach (var e in FurnitureCatalog.Home)
            {
                if (!categories.Contains(e.category))
                    categories.Add(e.category);
            }
            var chipRects = FlowChips(categories, width - 2 * Pad, out float chipsHeight);

            float gridY = 82f + chipsHeight + Gap;
            float pagerY = gridY + Rows * TileH + (Rows - 1) * Gap + Gap;
            float height = pagerY + 28f + Gap + FooterH + Pad;

            var canvas = UIKit.CreateWorldCanvas("Shopping List Canvas", new Vector2(width, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 24f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var title = UIKit.Label(p, "Title", "Shopping list", 24f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, width - 2 * Pad, 32f);
            var sub = UIKit.Label(p, "Subtitle", "Pick a size and add it to your room. Grab to move, use two hands to turn.",
                11.5f, UIKit.MutedTextColor);
            UIKit.Place(sub.rectTransform, Pad, Pad + 34f, width - 2 * Pad, 16f);

            for (int i = 0; i < categories.Count; i++)
                BuildChip(p, categories[i], chipRects[i]);

            for (int i = 0; i < PerPage; i++)
            {
                int col = i % Columns, row = i / Columns;
                BuildTile(p, i, Pad + col * (TileW + Gap), gridY + row * (TileH + Gap));
            }

            m_Prev = UIKit.Button(p, "Previous Page", "Previous", 11f, false, () => SetPage(m_Page - 1));
            UIKit.Place((RectTransform)m_Prev.transform, Pad, pagerY, 110f, 28f);
            m_Next = UIKit.Button(p, "Next Page", "Next", 11f, false, () => SetPage(m_Page + 1));
            UIKit.Place((RectTransform)m_Next.transform, width - Pad - 110f, pagerY, 110f, 28f);
            m_PageLabel = UIKit.Label(p, "Page", string.Empty, 11f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(m_PageLabel.rectTransform, Pad + 120f, pagerY, width - 2 * Pad - 240f, 28f);

            var close = UIKit.Button(p, "Close", "Close", 14f, false, Close);
            UIKit.Place((RectTransform)close.transform, Pad, height - Pad - FooterH + 6f, width - 2 * Pad, FooterH - 6f);

            m_Palettes.PaletteChanged += _ => Refresh();
            m_Room.Changed += Refresh;
            ApplyFilter(AllCategory);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, m_Head, distance, belowEyes);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // ---- filtering & paging -----------------------------------------------------------

        void ApplyFilter(string category)
        {
            m_Category = category;
            m_Visible.Clear();
            foreach (var e in FurnitureCatalog.Home)
            {
                if (category != AllCategory && e.category != category)
                    continue;
                foreach (var v in e.variants)
                    m_Visible.Add(new Option { entry = e, variant = v });
            }
            foreach (var chip in m_Chips)
            {
                bool on = chip.category == category;
                chip.button.colors = on ? UIKit.PrimaryColors() : UIKit.SecondaryColors();
                chip.label.color = on ? Color.white : UIKit.TextColor;
            }
            SetPage(0);
        }

        int PageCount => Mathf.Max(1, Mathf.CeilToInt(m_Visible.Count / (float)PerPage));

        void SetPage(int page)
        {
            m_Page = Mathf.Clamp(page, 0, PageCount - 1);
            Refresh();
        }

        void Refresh()
        {
            if (m_Tiles.Count == 0)
                return;
            var palette = m_Palettes.Current;
            for (int i = 0; i < m_Tiles.Count; i++)
            {
                var tile = m_Tiles[i];
                int index = m_Page * PerPage + i;
                bool shown = index < m_Visible.Count;
                tile.root.SetActive(shown);
                if (!shown)
                    continue;
                var option = m_Visible[index];
                var sprite = m_Room.Library != null ? m_Room.Library.IconFor(option.entry.kind, option.variant.id) : null;
                tile.icon.sprite = sprite;
                tile.icon.enabled = sprite != null;
                tile.name.text = option.entry.displayName;
                tile.size.text = option.variant.label;
                tile.dims.text = option.variant.dimensions;
                tile.dot.color = palette.Get(RoleSlot(option.entry.role));
                int n = m_Room.Count(option.entry.kind, option.variant.id);
                tile.count.text = n.ToString();
                tile.remove.interactable = n > 0; // no dead buttons
            }

            m_PageLabel.text = $"{m_Visible.Count} item{(m_Visible.Count == 1 ? "" : "s")} · page {m_Page + 1} of {PageCount}";
            // Hide paging entirely when everything fits on one page.
            m_Prev.gameObject.SetActive(PageCount > 1);
            m_Next.gameObject.SetActive(PageCount > 1);
            m_Prev.interactable = m_Page > 0;
            m_Next.interactable = m_Page < PageCount - 1;
        }

        void OnAdd(int tileIndex)
        {
            int index = m_Page * PerPage + tileIndex;
            if (index < m_Visible.Count)
                AddRequested?.Invoke(m_Visible[index].entry.kind, m_Visible[index].variant.id);
        }

        void OnRemove(int tileIndex)
        {
            int index = m_Page * PerPage + tileIndex;
            if (index < m_Visible.Count)
                m_Room.RemoveLast(m_Visible[index].entry.kind, m_Visible[index].variant.id);
        }

        // ---- building ---------------------------------------------------------------------

        /// <summary>Wraps chips left-to-right into rows; returns each chip's rect relative to the chip area.</summary>
        static List<Rect> FlowChips(List<string> labels, float maxWidth, out float totalHeight)
        {
            const float chipGap = 6f, charWidth = 5.6f, padding = 18f;
            var rects = new List<Rect>();
            float x = 0f, y = 0f;
            foreach (var label in labels)
            {
                float w = padding + label.Length * charWidth;
                if (x > 0f && x + w > maxWidth)
                {
                    x = 0f;
                    y += ChipH + chipGap;
                }
                rects.Add(new Rect(x, y, w, ChipH));
                x += w + chipGap;
            }
            totalHeight = y + ChipH;
            return rects;
        }

        void BuildChip(Transform parent, string category, Rect rect)
        {
            var button = UIKit.Button(parent, $"Filter {category}", category, 10f, false, () => ApplyFilter(category));
            UIKit.Place((RectTransform)button.transform, Pad + rect.x, 82f + rect.y, rect.width, rect.height);
            m_Chips.Add(new Chip { category = category, button = button, label = button.GetComponentInChildren<TextMeshProUGUI>() });
        }

        void BuildTile(Transform parent, int index, float x, float y)
        {
            var card = UIKit.Panel(parent, $"Tile {index + 1}", new Color(1f, 1f, 1f, 0.75f), 10f);
            card.raycastTarget = false;
            UIKit.Place(card.rectTransform, x, y, TileW, TileH);
            var c = card.transform;
            var tile = new Tile { root = card.gameObject };

            tile.icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            tile.icon.transform.SetParent(c, false);
            tile.icon.preserveAspect = true;
            tile.icon.raycastTarget = false;
            UIKit.Place(tile.icon.rectTransform, (TileW - Icon) / 2f, 4f, Icon, Icon);

            // Role color in the corner: the palette slot this piece will take.
            tile.dot = UIKit.Panel(c, "Role Color", Color.white, 4.5f);
            tile.dot.raycastTarget = false;
            UIKit.Place(tile.dot.rectTransform, 7f, 7f, 9f, 9f);

            tile.name = UIKit.Label(c, "Name", string.Empty, 11f, UIKit.TextColor, TextAlignmentOptions.Top, FontStyles.Bold);
            Fit(tile.name, 9f, 11f);
            UIKit.Place(tile.name.rectTransform, 4f, 76f, TileW - 8f, 14f);
            tile.size = UIKit.Label(c, "Size", string.Empty, 9.5f, UIKit.TextColor, TextAlignmentOptions.Top);
            Fit(tile.size, 8f, 9.5f);
            UIKit.Place(tile.size.rectTransform, 4f, 91f, TileW - 8f, 12f);
            tile.dims = UIKit.Label(c, "Dimensions", string.Empty, 8.5f, UIKit.MutedTextColor, TextAlignmentOptions.Top);
            Fit(tile.dims, 7f, 8.5f);
            UIKit.Place(tile.dims.rectTransform, 4f, 104f, TileW - 8f, 11f);

            int i = index;
            tile.remove = UIKit.Button(c, "Remove", "−", 16f, false, () => OnRemove(i));
            UIKit.Place((RectTransform)tile.remove.transform, 6f, TileH - 32f, 28f, 26f);
            tile.count = UIKit.Label(c, "Count", "0", 13f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(tile.count.rectTransform, 34f, TileH - 32f, TileW - 68f, 26f);
            var add = UIKit.Button(c, "Add", "+", 16f, true, () => OnAdd(i));
            UIKit.Place((RectTransform)add.transform, TileW - 34f, TileH - 32f, 28f, 26f);

            m_Tiles.Add(tile);
        }

        static void Fit(TextMeshProUGUI label, float min, float max)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = max;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        static PaletteSlot RoleSlot(FurnitureRole role)
        {
            switch (role)
            {
                case FurnitureRole.Dominant: return PaletteSlot.Dominant;
                case FurnitureRole.Secondary: return PaletteSlot.Secondary;
                default: return PaletteSlot.Accent;
            }
        }
    }
}
