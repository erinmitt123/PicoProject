using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The home catalog as a filterable, scrolling grid: one tile per size of each piece, with a baked
    /// picture, its size, its 60-30-10 role color, how many are in the room, and add/remove buttons.
    /// Pictures are flat sprites, so a tile costs a quad and a small texture.
    /// </summary>
    public class ShoppingListPanel : MonoBehaviour
    {
        const float Pad = 24f, TileW = 96f, TileH = 156f, Gap = 10f, FooterH = 44f, Icon = 70f, ChipH = 24f;
        const int Columns = 5;
        const string AllCategory = "All";

        [SerializeField] float distance = 0.8f;
        [SerializeField] float belowEyes = 0.15f;

        class Tile
        {
            public FurnitureCatalog.Entry entry;
            public FurnitureVariant variant;
            public GameObject root;
            public Image dot;
            public TextMeshProUGUI count;
            public Button remove, add;
            public RectTransform picture;
        }

        readonly List<Tile> m_Tiles = new List<Tile>();
        readonly List<(string category, Button chip)> m_Chips = new List<(string, Button)>();
        PaletteManager m_Palettes;
        RoomFurniture m_Room;
        Transform m_Head;
        RectTransform m_Grid;
        TextMeshProUGUI m_Summary;
        Button m_Close;
        string m_Filter = AllCategory;

        /// <summary>The Close button (for the tutorial to point at).</summary>
        public RectTransform CloseButton => m_Close != null ? (RectTransform)m_Close.transform : null;

        public event Action<FurnitureKind, string> AddRequested;

        /// <summary>A tile's picture was held: add the piece and put it in this hand (null when there's no XR hand, e.g. the mouse).</summary>
        public event Action<FurnitureKind, string, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor> PickUpRequested;
        public event Action Closed;

        public void Build(Camera eventCamera, PaletteManager palettes, RoomFurniture room)
        {
            m_Palettes = palettes;
            m_Room = room;
            m_Head = eventCamera.transform;

            const float scrollbar = 14f;
            float width = 2 * Pad + Columns * TileW + (Columns - 1) * Gap + scrollbar;

            var categories = new List<string> { AllCategory };
            foreach (var e in FurnitureCatalog.Home)
            {
                if (!categories.Contains(e.category))
                    categories.Add(e.category);
            }
            var chipRects = UIKit.FlowChips(categories, width - 2 * Pad, ChipH, 10f, out float chipsHeight);

            const float chipsY = 82f;
            float gridY = chipsY + chipsHeight + Gap;
            float gridH = 2.45f * (TileH + Gap); // the cut-off third row signals that the list scrolls
            float summaryY = gridY + gridH + 6f;
            float height = summaryY + 18f + Gap + FooterH + Pad;

            var canvas = UIKit.CreateWorldCanvas("Shopping List Canvas", new Vector2(width, height), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 24f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var title = UIKit.Label(p, "Title", "Shopping list", 24f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, width - 2 * Pad, 32f);
            var sub = UIKit.Label(p, "Subtitle", "Pinch a picture and drag it into your room, or press + to place it in front of you.",
                11.5f, UIKit.MutedTextColor);
            UIKit.Place(sub.rectTransform, Pad, Pad + 34f, width - 2 * Pad, 16f);

            for (int i = 0; i < categories.Count; i++)
            {
                string category = categories[i];
                var chip = UIKit.Button(p, $"Filter {category}", category, 10f, false, () => ApplyFilter(category));
                var r = chipRects[i];
                UIKit.Place((RectTransform)chip.transform, Pad + r.x, chipsY + r.y, r.width, r.height);
                m_Chips.Add((category, chip));
            }

            m_Grid = UIKit.ScrollGrid(p, "Catalog Grid", Pad, gridY, width - 2 * Pad, gridH, new Vector2(TileW, TileH), Gap, Columns);
            foreach (var e in FurnitureCatalog.Home)
                foreach (var v in e.variants)
                    BuildTile(e, v);

            m_Summary = UIKit.Label(p, "Summary", string.Empty, 10.5f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(m_Summary.rectTransform, Pad, summaryY, width - 2 * Pad, 18f);

            var close = m_Close = UIKit.Button(p, "Close", "Close", 14f, false, Close);
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

        /// <summary>
        /// Shows <paramref name="kind"/>'s category, and returns the picture of its usual size (for the tutorial
        /// to point at while teaching the drag), or null when the list is closed.
        /// </summary>
        public RectTransform FocusOn(FurnitureKind kind)
        {
            if (!isActiveAndEnabled)
                return null;
            var entry = FurnitureCatalog.Get(kind);
            if (m_Filter != entry.category)
                ApplyFilter(entry.category);
            foreach (var tile in m_Tiles)
            {
                if (tile.entry.kind == kind && tile.variant.id == entry.Default.id)
                    return tile.picture != null ? tile.picture : (RectTransform)tile.add.transform;
            }
            return null;
        }

        void ApplyFilter(string category)
        {
            m_Filter = category;
            int shown = 0;
            foreach (var tile in m_Tiles)
            {
                bool show = category == AllCategory || tile.entry.category == category;
                tile.root.SetActive(show);
                if (show)
                    shown++;
            }
            foreach (var (c, chip) in m_Chips)
                UIKit.SetChipSelected(chip, c == category);
            m_Summary.Localize(shown == 1 ? "1 item" : "{0} items", shown);
            m_Grid.anchoredPosition = Vector2.zero; // back to the top of the list
            Refresh();
        }

        void Refresh()
        {
            var palette = m_Palettes.Current;
            foreach (var tile in m_Tiles)
            {
                tile.dot.color = palette.Get(RoleSlot(tile.entry.role));
                int n = m_Room.Count(tile.entry.kind, tile.variant.id);
                tile.count.SetPlain(n.ToString());
                tile.remove.interactable = n > 0; // no dead buttons
            }
        }

        void BuildTile(FurnitureCatalog.Entry entry, FurnitureVariant variant)
        {
            var card = UIKit.Panel(m_Grid, $"{entry.displayName} {variant.label}", new Color(1f, 1f, 1f, 0.75f), 10f);
            card.raycastTarget = false;
            var c = card.transform;
            var tile = new Tile { entry = entry, variant = variant, root = card.gameObject };

            var sprite = m_Room.Library != null ? m_Room.Library.IconFor(entry.kind, variant.id) : null;
            if (sprite != null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                icon.transform.SetParent(c, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                UIKit.Place(icon.rectTransform, (TileW - Icon) / 2f, 4f, Icon, Icon);
                // Hold the picture to lift the piece out of the list and carry it into the room.
                icon.raycastTarget = true;
                icon.gameObject.AddComponent<TilePickUp>().PickedUp += hand => PickUpRequested?.Invoke(entry.kind, variant.id, hand);
                tile.picture = icon.rectTransform;
            }

            // Role color in the corner: the palette slot this piece will take.
            tile.dot = UIKit.Panel(c, "Role Color", Color.white, 4.5f);
            tile.dot.raycastTarget = false;
            UIKit.Place(tile.dot.rectTransform, 7f, 7f, 9f, 9f);

            var name = UIKit.Label(c, "Name", entry.displayName, 11f, UIKit.TextColor, TextAlignmentOptions.Top, FontStyles.Bold);
            Fit(name, 9f, 11f);
            UIKit.Place(name.rectTransform, 4f, 76f, TileW - 8f, 14f);
            var size = UIKit.Label(c, "Size", variant.label, 9.5f, UIKit.TextColor, TextAlignmentOptions.Top);
            Fit(size, 8f, 9.5f);
            UIKit.Place(size.rectTransform, 4f, 91f, TileW - 8f, 12f);
            var dims = UIKit.Label(c, "Dimensions", variant.dimensions, 8.5f, UIKit.MutedTextColor, TextAlignmentOptions.Top);
            Fit(dims, 7f, 8.5f);
            UIKit.Place(dims.rectTransform, 4f, 104f, TileW - 8f, 11f);

            tile.remove = UIKit.Button(c, "Remove", "−", 16f, false, () => m_Room.RemoveLast(entry.kind, variant.id));
            UIKit.Place((RectTransform)tile.remove.transform, 6f, TileH - 32f, 28f, 26f);
            tile.count = UIKit.Label(c, "Count", "0", 13f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(tile.count.rectTransform, 34f, TileH - 32f, TileW - 68f, 26f);
            var add = tile.add = UIKit.Button(c, "Add", "+", 16f, true, () => AddRequested?.Invoke(entry.kind, variant.id));
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
