using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Small runtime uGUI builder for the Palette Room look: frosted panels, rounded corners,
    /// and buttons with hover/press states. World canvases use 1 unit = 1 mm.
    /// </summary>
    public static class UIKit
    {
        public const float MillimetresToMetres = 0.001f;

        public static readonly Color PanelColor = new Color(0.97f, 0.96f, 0.94f, 0.9f);
        public static readonly Color TextColor = new Color(0.16f, 0.16f, 0.17f, 1f);
        public static readonly Color MutedTextColor = new Color(0.38f, 0.38f, 0.40f, 1f);
        public static readonly Color PrimaryColor = new Color(0.17f, 0.20f, 0.24f, 1f);
        public static readonly Color SecondaryColor = new Color(1f, 1f, 1f, 0.75f);

        /// <summary>
        /// Editor test mode: also accept mouse clicks. XRI's tracked-device raycaster ignores the mouse,
        /// so canvases built while this is set get a standard GraphicRaycaster as well.
        /// </summary>
        public static bool MouseInput { get; set; }

        static Sprite s_Rounded;

        /// <summary>A 9-sliced rounded-rect sprite; corner size is set per Image via pixelsPerUnitMultiplier.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (s_Rounded != null)
                    return s_Rounded;
                const int size = 64, radius = 24;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "PaletteRoom Rounded",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Distance outside the rounded rect, antialiased over one pixel.
                        float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                        float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                        float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                // 100 PPU matches the canvas reference PPU, so border pixels map 1:1 to canvas units
                // before pixelsPerUnitMultiplier scales them.
                s_Rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                s_Rounded.name = "PaletteRoom Rounded";
                return s_Rounded;
            }
        }

        public static Canvas CreateWorldCanvas(string name, Vector2 sizeMm, Transform parent, Camera eventCamera)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            // Handles poke, near/far ray and controller trigger through the XR UI Input Module.
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (MouseInput)
                go.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * MillimetresToMetres;
            return canvas;
        }

        /// <summary>Puts a panel <paramref name="distance"/> in front of the user, slightly below eye level, facing them.</summary>
        public static void PlaceInFront(Transform panel, Transform head, float distance, float belowEyes)
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();
            panel.position = head.position + forward * distance + Vector3.down * belowEyes;
            panel.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Places <paramref name="rt"/> by its top-left corner, in parent millimetres from the parent's top-left.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, float cornerMm)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 24f / Mathf.Max(cornerMm, 0.01f);
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float sizeMm, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = sizeMm;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string text, float textSizeMm, bool primary, UnityAction onClick)
        {
            var bg = Panel(parent, name, Color.white, 14f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.colors = primary ? PrimaryColors() : SecondaryColors();
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var label = Label(bg.transform, "Label", text, textSizeMm, primary ? Color.white : TextColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform, 6f);
            return button;
        }

        /// <summary>
        /// A vertically scrolling grid inside <paramref name="parent"/> at the given rect (mm from the
        /// parent's top-left). Drag anywhere in it (ray, poke or mouse) or grab the scrollbar to scroll.
        /// Children added to the returned content are laid out by its GridLayoutGroup; inactive children
        /// are skipped, so filtering is just toggling them.
        /// </summary>
        public static RectTransform ScrollGrid(Transform parent, string name, float x, float y, float width, float height,
            Vector2 cell, float spacing, int columns)
        {
            const float barWidth = 8f, barGap = 6f;
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            Place(root, x, y, width, height);

            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-(barWidth + barGap), 0f);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // invisible, but catches drags in the gaps between items
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var track = Panel(root, "Scrollbar", new Color(0f, 0f, 0f, 0.08f), barWidth / 2f);
            track.rectTransform.anchorMin = new Vector2(1f, 0f);
            track.rectTransform.anchorMax = new Vector2(1f, 1f);
            track.rectTransform.pivot = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(barWidth, 0f);
            track.rectTransform.anchoredPosition = Vector2.zero;
            var area = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(track.transform, false);
            Stretch(area);
            var handle = Panel(area, "Handle", new Color(0.17f, 0.20f, 0.24f, 0.55f), barWidth / 2f);
            Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 25f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>Lays chips out left-to-right, wrapping into rows; returns each chip's rect (mm) within the chip area.</summary>
        public static List<Rect> FlowChips(IReadOnlyList<string> labels, float maxWidth, float chipHeight, float textSize, out float totalHeight)
        {
            const float gap = 6f;
            float charWidth = textSize * 0.66f, padding = textSize * 2f;
            var rects = new List<Rect>();
            float x = 0f, y = 0f;
            foreach (var label in labels)
            {
                float w = padding + label.Length * charWidth;
                if (x > 0f && x + w > maxWidth)
                {
                    x = 0f;
                    y += chipHeight + gap;
                }
                rects.Add(new Rect(x, y, w, chipHeight));
                x += w + gap;
            }
            totalHeight = y + chipHeight;
            return rects;
        }

        /// <summary>Shows a filter chip as selected (dark) or not (light).</summary>
        public static void SetChipSelected(Button chip, bool selected)
        {
            chip.colors = selected ? PrimaryColors() : SecondaryColors();
            var label = chip.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null)
                return;
            label.color = selected ? Color.white : TextColor;
            // Chip labels stay on one line; long ones shrink a little rather than wrap.
            if (!label.enableAutoSizing)
            {
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * 0.75f;
                label.enableAutoSizing = true;
            }
        }

        public static ColorBlock PrimaryColors() => new ColorBlock
        {
            normalColor = PrimaryColor,
            highlightedColor = new Color(0.27f, 0.31f, 0.37f, 1f),
            pressedColor = new Color(0.08f, 0.09f, 0.11f, 1f),
            selectedColor = PrimaryColor,
            disabledColor = new Color(0.17f, 0.20f, 0.24f, 0.4f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        public static ColorBlock SecondaryColors() => new ColorBlock
        {
            normalColor = SecondaryColor,
            highlightedColor = Color.white,
            pressedColor = new Color(0.85f, 0.85f, 0.86f, 1f),
            selectedColor = SecondaryColor,
            disabledColor = new Color(1f, 1f, 1f, 0.3f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };
    }
}
