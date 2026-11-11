using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
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

        public static readonly Color PanelColor = new Color(0.97f, 0.96f, 0.94f, 1f);
        /// <summary>Dialogs that open on top of other panels are solid, so what's behind doesn't show through the text.</summary>
        public static readonly Color ModalPanelColor = new Color(0.97f, 0.96f, 0.94f, 1f);
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
        const int RoundedRadiusPx = 48;

        /// <summary>A 9-sliced rounded-rect sprite; corner size is set per Image via pixelsPerUnitMultiplier.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (s_Rounded != null)
                    return s_Rounded;
                const int size = 128, radius = RoundedRadiusPx;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "PaletteRoom Rounded",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
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
                tex.Apply(true, true);
                // 100 PPU matches the canvas reference PPU, so border pixels map 1:1 to canvas units
                // before pixelsPerUnitMultiplier scales them.
                s_Rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                s_Rounded.name = "PaletteRoom Rounded";
                return s_Rounded;
            }
        }

        static Sprite s_Star;
        static readonly Color StarOn = new Color(0.93f, 0.66f, 0.16f);
        static readonly Color StarOff = new Color(0.82f, 0.81f, 0.79f);

        /// <summary>A five-point star, drawn in code (the UI font has no star glyph).</summary>
        public static Sprite StarSprite
        {
            get
            {
                if (s_Star != null)
                    return s_Star;
                const int size = 64, samples = 4;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                var points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? 0.48f : 0.2f;
                    points[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
                }
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        var p = new Vector2((x + (sx + 0.5f) / samples) / size, (y + (sy + 0.5f) / samples) / size);
                        if (InPolygon(p, points))
                            inside++;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                s_Star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                s_Star.name = "PaletteRoom Star";
                return s_Star;
            }
        }

        static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Three stars in a row at (x, y), each <paramref name="size"/> mm; set how many are earned with <see cref="SetStars"/>.</summary>
        public static Image[] StarRow(Transform parent, float x, float y, float size)
        {
            var row = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Star {i + 1}", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var image = go.AddComponent<Image>();
                image.sprite = StarSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = StarOff;
                Place(image.rectTransform, x + i * (size + 2f), y, size, size);
                row[i] = image;
            }
            return row;
        }

        public static void SetStars(Image[] row, int earned)
        {
            for (int i = 0; i < row.Length; i++)
                row[i].color = i < earned ? StarOn : StarOff;
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
            // Drawn over the real room, so walls and furniture never hide the interface. Added last: it
            // remembers the canvas scale for its pop-in.
            go.AddComponent<OverlayCanvas>();
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

        /// <summary>
        /// A faint edge round a color swatch, so pale colors (cream, white) still read as a chip on a
        /// pale panel instead of vanishing into it.
        /// </summary>
        public static Image Edge(Image swatch)
        {
            var outline = swatch.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return swatch;
        }

        public static Image Panel(Transform parent, string name, Color color, float cornerMm)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = RoundedRadiusPx / Mathf.Max(cornerMm, 0.01f);
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float sizeMm, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (HasLetter(text) && text.Length > 1) // single letters are keyboard keys
                label.Localize(text);
            else
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
            bg.gameObject.AddComponent<ForgivingClick>();

            var label = Label(bg.transform, "Label", text, textSizeMm, primary ? Color.white : TextColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform, 6f);
            return button;
        }

        /// <summary>A button with an icon (see <see cref="Icons"/>) before its label.</summary>
        public static Button Button(Transform parent, string name, string text, float textSizeMm, bool primary, UnityAction onClick, string icon)
        {
            var button = Button(parent, name, text, textSizeMm, primary, onClick);
            if (string.IsNullOrEmpty(icon))
                return button;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            // Icon and text sit together in the middle: the label makes room on the left for the glyph.
            float iconSize = textSizeMm * 1.45f;
            var glyph = Label(button.transform, "Icon", icon, iconSize, primary ? Color.white : TextColor, TextAlignmentOptions.Center);
            glyph.textWrappingMode = TextWrappingModes.NoWrap;
            var gr = glyph.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0f, 0.5f);
            gr.pivot = new Vector2(0f, 0.5f);
            gr.sizeDelta = new Vector2(iconSize * 1.1f, iconSize * 1.1f);
            gr.anchoredPosition = new Vector2(textSizeMm * 0.8f, 0f);
            label.rectTransform.offsetMin = new Vector2(textSizeMm * 0.8f + iconSize * 1.2f, label.rectTransform.offsetMin.y);
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        /// <summary>
        /// A square tile for a side rail: a large icon over a short caption. The caption is optional;
        /// without one the icon is centred.
        /// </summary>
        public static Button IconTile(Transform parent, string name, string icon, string caption, float sizeMm, UnityAction onClick, bool primary = false)
        {
            var bg = Panel(parent, name, Color.white, sizeMm * 0.26f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.colors = primary ? PrimaryColors() : SecondaryColors();
            if (onClick != null)
                button.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ForgivingClick>();
            var color = primary ? Color.white : TextColor;
            bool hasCaption = !string.IsNullOrEmpty(caption);
            var glyph = Label(bg.transform, "Icon", icon, sizeMm * (hasCaption ? 0.42f : 0.55f), color, TextAlignmentOptions.Center);
            glyph.textWrappingMode = TextWrappingModes.NoWrap;
            var gr = glyph.rectTransform;
            gr.anchorMin = new Vector2(0f, hasCaption ? 0.36f : 0f);
            gr.anchorMax = Vector2.one;
            gr.offsetMin = gr.offsetMax = Vector2.zero;
            if (hasCaption)
            {
                var label = Label(bg.transform, "Label", caption, sizeMm * 0.15f, color, TextAlignmentOptions.Center, FontStyles.Bold);
                label.enableAutoSizing = true;
                label.fontSizeMax = sizeMm * 0.15f;
                label.fontSizeMin = sizeMm * 0.1f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = new Vector2(1f, 0.38f);
                lr.offsetMin = new Vector2(3f, 3f);
                lr.offsetMax = new Vector2(-3f, 0f);
            }
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
            var icon = chip.transform.Find("Icon");
            if (icon != null && icon.TryGetComponent(out TextMeshProUGUI glyph))
                glyph.color = label.color;
            // Chip labels stay on one line; long ones shrink a little rather than wrap.
            if (!label.enableAutoSizing)
            {
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * 0.75f;
                label.enableAutoSizing = true;
            }
        }

        static bool HasLetter(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (char c in text)
                if (char.IsLetter(c))
                    return true;
            return false;
        }

        // ---- drawing over the real room ---------------------------------------------------------

        static readonly Dictionary<Material, Material> s_Overlays = new Dictionary<Material, Material>();
        static readonly HashSet<Material> s_OverlaySet = new HashSet<Material>();

        /// <summary>Makes a UI or text material draw on top of everything, whatever is in front of it.</summary>
        public static void MakeOverlay(Material material)
        {
            if (material == null)
                return;
            // UI/Default and the TextMesh Pro UI shaders read their depth test from this property.
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            s_OverlaySet.Add(material);
        }

        /// <summary>An always-on-top copy of <paramref name="source"/> (shared, made once per material).</summary>
        public static Material OverlayOf(Material source)
        {
            if (source == null || s_OverlaySet.Contains(source))
                return source;
            if (s_Overlays.TryGetValue(source, out var overlay) && overlay != null)
                return overlay;
            overlay = new Material(source) { name = source.name + " (Overlay)" };
            MakeOverlay(overlay);
            s_Overlays[source] = overlay;
            return overlay;
        }

        public static bool IsOverlay(Material material) => material != null && s_OverlaySet.Contains(material);

        // ---- windows ---------------------------------------------------------------------------

        static readonly List<Image[]> s_Stripes = new List<Image[]>();
        static readonly Color[] s_Theme =
        {
            new Color(0.36f, 0.45f, 0.42f), new Color(0.80f, 0.70f, 0.58f), new Color(0.80f, 0.45f, 0.33f),
            new Color(0.90f, 0.87f, 0.82f), new Color(0.95f, 0.78f, 0.38f),
        };

        /// <summary>The current palette's five colors (Dominant to Highlight), for accents.</summary>
        public static Color ThemeColor(int i) => s_Theme[Mathf.Clamp(i, 0, s_Theme.Length - 1)];

        /// <summary>
        /// A panel's background: frosted card with a soft shadow behind it and a little strip of the
        /// current palette's colors under it, so every window carries the room's colors. Fills
        /// <paramref name="parent"/>; returns the card to build on.
        /// </summary>
        public static Image Window(Transform parent, float cornerMm, bool modal = false)
        {
            var shadow = new GameObject("Shadow", typeof(RectTransform)).AddComponent<Image>();
            shadow.transform.SetParent(parent, false);
            shadow.sprite = ShadowSprite;
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 1f;
            shadow.color = new Color(0f, 0f, 0f, modal ? 0.32f : 0.22f);
            shadow.raycastTarget = false;
            var srt = shadow.rectTransform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(-16f, -24f);
            srt.offsetMax = new Vector2(16f, 10f);

            var card = Panel(parent, "Panel", modal ? ModalPanelColor : PanelColor, cornerMm);
            Stretch(card.rectTransform);

            // Five swatches just under the card's bottom edge.
            var strip = new GameObject("Palette Strip", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(parent, false);
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 0f);
            strip.pivot = new Vector2(0.5f, 1f);
            const float swatchW = 18f, swatchH = 4f, gap = 3f;
            strip.sizeDelta = new Vector2(5 * swatchW + 4 * gap, swatchH);
            strip.anchoredPosition = new Vector2(0f, -5f);
            var swatches = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var s = Panel(strip, $"Swatch {i + 1}", s_Theme[i], swatchH / 2f);
                s.raycastTarget = false;
                Place(s.rectTransform, i * (swatchW + gap), 0f, swatchW, swatchH);
                swatches[i] = s;
            }
            s_Stripes.Add(swatches);

            var overlay = parent.GetComponentInParent<OverlayCanvas>();
            if (overlay != null)
                overlay.IsWindow = true;
            return card;
        }

        /// <summary>Recolors every window's palette strip to the palette in use.</summary>
        public static void SetThemePalette(ColorPalette palette)
        {
            if (palette == null)
                return;
            var slots = (PaletteSlot[])System.Enum.GetValues(typeof(PaletteSlot));
            for (int i = 0; i < 5 && i < slots.Length; i++)
                s_Theme[i] = palette.Get(slots[i]);
            for (int n = s_Stripes.Count - 1; n >= 0; n--)
            {
                var row = s_Stripes[n];
                if (row[0] == null)
                {
                    s_Stripes.RemoveAt(n);
                    continue;
                }
                for (int i = 0; i < row.Length; i++)
                    row[i].color = s_Theme[i];
            }
        }

        static Sprite s_Shadow, s_Outline, s_Arrow;

        /// <summary>A soft-edged rounded rect for drop shadows (9-sliced).</summary>
        public static Sprite ShadowSprite
        {
            get
            {
                if (s_Shadow != null)
                    return s_Shadow;
                const int size = 64, radius = 6, blur = 20, border = radius + blur;
                s_Shadow = SpriteFromField("PaletteRoom Shadow", size, border, (x, y) =>
                {
                    float dx = Mathf.Max(border - x, x - (size - border), 0f);
                    float dy = Mathf.Max(border - y, y - (size - border), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 inside the solid core
                    float t = Mathf.Clamp01(1f - d / blur);
                    return t * t * (3f - 2f * t);
                });
                return s_Shadow;
            }
        }

        /// <summary>A rounded-rect ring for the tutorial's highlight around a target (9-sliced).</summary>
        public static Sprite OutlineSprite
        {
            get
            {
                if (s_Outline != null)
                    return s_Outline;
                const int size = 64, radius = 24;
                const float thickness = 5f;
                s_Outline = SpriteFromField("PaletteRoom Outline", size, radius, (x, y) =>
                {
                    float dx = Mathf.Max(radius - x, x - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y, y - (size - radius), 0f);
                    float inside = radius - Mathf.Sqrt(dx * dx + dy * dy); // distance in from the edge
                    return Mathf.Clamp01(inside + 0.5f) * Mathf.Clamp01(thickness - inside + 0.5f);
                });
                return s_Outline;
            }
        }

        /// <summary>A chunky arrow pointing down, for showing the user where to look.</summary>
        public static Sprite ArrowSprite
        {
            get
            {
                if (s_Arrow != null)
                    return s_Arrow;
                const int size = 64;
                s_Arrow = SpriteFromField("PaletteRoom Arrow", size, 0, (x, y) =>
                {
                    float u = x / size, v = y / size; // v = 0 at the bottom (the tip)
                    float half = Mathf.Abs(u - 0.5f);
                    float head = v < 0.55f ? (v * 0.85f - half) * size : -1f; // triangle, tip at the bottom
                    float shaft = v >= 0.45f && v < 0.98f ? (0.16f - half) * size : -1f;
                    return Mathf.Clamp01(Mathf.Max(head, shaft) + 0.5f);
                }, samples: 4);
                return s_Arrow;
            }
        }

        static Sprite SpriteFromField(string name, int size, int border, System.Func<float, float, float> alpha, int samples = 1)
        {
            bool pot = Mathf.IsPowerOfTwo(size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, pot)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = pot ? FilterMode.Trilinear : FilterMode.Bilinear,
                anisoLevel = 4,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                    a += alpha(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a / (samples * samples))));
            }
            tex.SetPixels32(pixels);
            tex.Apply(pot, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
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
