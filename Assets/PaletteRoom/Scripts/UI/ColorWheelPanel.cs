using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Build-your-own palette: pick a base color on an RYB color wheel, choose one of the harmony
    /// suggestions built from it, optionally retouch individual colors, then "Done" restyles the room.
    /// Reopening restores the user's palette so it can keep being changed; the set colors are kept between
    /// sessions too.
    /// </summary>
    public class ColorWheelPanel : MonoBehaviour
    {
        const float W = 600f, H = 530f, Pad = 24f;
        const float WheelSize = 230f, WheelY = 84f, StripY = 326f;
        const float RightX = Pad + WheelSize + 24f, RowY = 108f, RowH = 40f, RowGap = 6f;
        const float SlotY = 440f, SlotW = 76f, SlotH = 44f, SlotGap = 8f;

        static readonly Color Ring = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly PaletteSlot[] Slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));

        [SerializeField] float distance = 0.75f;
        [SerializeField] float belowEyes = 0.12f;

        PaletteManager m_Palettes;
        Transform m_Head;

        Image m_Wheel;
        RectTransform m_WheelMarker;
        Image m_Strip;
        Texture2D m_StripTexture;
        RectTransform m_StripMarker;
        TextMeshProUGUI m_Hint;
        readonly List<Image[]> m_RowSwatches = new List<Image[]>();
        readonly List<Image> m_RowRings = new List<Image>();
        readonly List<TextMeshProUGUI> m_RowNames = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> m_RowNotes = new List<TextMeshProUGUI>();
        readonly Image[] m_SlotSwatches = new Image[Slots.Length];
        readonly Image[] m_SlotRings = new Image[Slots.Length];
        readonly TextMeshProUGUI[] m_SlotLabels = new TextMeshProUGUI[Slots.Length];

        // Picker state. Hue is on the RYB wheel in degrees; saturation/value are HSV 0..1.
        float m_Hue, m_Sat = 0.5f, m_Val = 0.85f;
        // Colors the user has pinned, in the order they set them; suggestions are built around these.
        readonly List<ColorHarmony.SetColor> m_Set = new List<ColorHarmony.SetColor>();
        int m_NextOrder;
        List<ColorHarmony.Suggestion> m_Suggestions = new List<ColorHarmony.Suggestion>();
        int m_Selected = -1;
        Color m_Seed;
        ColorPalette m_Working;
        int m_EditSlot;

        /// <summary>Raised when the panel closes, whether by Close or Done.</summary>
        public event Action Closed;


        public void Build(Camera eventCamera, PaletteManager palettes)
        {
            m_Palettes = palettes;
            m_Head = eventCamera.transform;

            var canvas = UIKit.CreateWorldCanvas("Color Wheel Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 24f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var title = UIKit.Label(p, "Title", "Color wheel", 26f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, 360f, 34f);
            var close = UIKit.Button(p, "Close", "Close", 13f, false, Close);
            UIKit.Place((RectTransform)close.transform, W - Pad - 100f, Pad, 100f, 34f);
            var sub = UIKit.Label(p, "Subtitle", "Point at the wheel and pinch or pull the trigger to pick a color.",
                12f, UIKit.MutedTextColor);
            UIKit.Place(sub.rectTransform, Pad, Pad + 38f, W - 2 * Pad, 18f);

            BuildWheel(p);
            BuildStrip(p);

            m_Hint = UIKit.Label(p, "Hint", string.Empty, 11f, UIKit.TextColor);
            UIKit.Place(m_Hint.rectTransform, Pad, StripY + 48f, WheelSize, 32f);

            var suggestions = UIKit.Label(p, "Suggestions", "Colors that go with it", 14f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(suggestions.rectTransform, RightX, WheelY, W - RightX - Pad, 20f);
            for (int i = 0; i < ColorHarmony.AllRules.Length; i++)
                BuildRow(p, i, RowY + i * (RowH + RowGap));

            var yours = UIKit.Label(p, "Your Palette", "Your palette · tap a color, then pick on the wheel to set it", 12f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(yours.rectTransform, Pad, SlotY - 22f, 420f, 18f);
            for (int i = 0; i < Slots.Length; i++)
                BuildSlot(p, i);

            var clear = UIKit.Button(p, "Clear Set Colors", "Clear set colors", 10f, false, ClearSetColors);
            UIKit.Place((RectTransform)clear.transform, W - Pad - 120f, SlotY - 26f, 120f, 22f);

            var done = UIKit.Button(p, "Done", "Done", 16f, true, Done);
            UIKit.Place((RectTransform)done.transform, W - Pad - 120f, SlotY, 120f, SlotH);

            m_Set.AddRange(SaveData.WheelColors());
            foreach (var c in m_Set)
                m_NextOrder = Mathf.Max(m_NextOrder, c.order);
        }

        /// <summary>Shows the panel in front of the user, restoring their palette if they have one.</summary>
        public void Open()
        {
            // The pinned colors persist between visits so the user can keep refining their palette.
            var source = m_Palettes.Custom ?? m_Palettes.Current;
            m_Working = source.Clone();
            m_Seed = source.dominant;
            m_Selected = -1;
            m_EditSlot = 0;
            ShowSlotOnWheel();
            Recompute();

            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, m_Head, distance, belowEyes);
            RefreshAll();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Done()
        {
            var palette = m_Working.Clone();
            if (m_Selected >= 0 && m_Selected < m_Suggestions.Count)
                palette.harmony = m_Suggestions[m_Selected].rule;
            m_Palettes.SetCustomPalette(palette);
            Close();
        }

        // ---- input ---------------------------------------------------------------------------

        void OnWheelPicked(Vector2 n)
        {
            float r = n.magnitude;
            if (r > 1.05f)
                return; // outside the disc
            // Red at the top, hues running clockwise like a printed color wheel.
            float hue = 90f - Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
            m_Hue = Mathf.Repeat(hue, 360f);
            m_Sat = Mathf.Clamp01(r);
            ApplyPick();
        }

        void OnStripPicked(Vector2 n)
        {
            m_Val = Mathf.Clamp01((n.x + 1f) * 0.5f);
            ApplyPick();
        }

        /// <summary>Pins the picked color to the slot being edited and takes the best suggestion that keeps it.</summary>
        void ApplyPick()
        {
            var slot = Slots[m_EditSlot];
            var color = ColorHarmony.FromRyb(m_Hue, m_Sat, m_Val);
            m_Set.RemoveAll(s => s.slot == slot);
            m_Set.Add(new ColorHarmony.SetColor(slot, color, ++m_NextOrder));
            SaveSetColors();
            Recompute();
            m_Selected = 0;
            m_Working = m_Suggestions[0].palette.Clone();
            RefreshAll();
        }

        /// <summary>
        /// Takes a suggestion. If it had to replace some of the user's set colors to stay harmonious,
        /// those slots are no longer pinned. The list itself isn't re-sorted until the next pick, so rows
        /// don't jump under the user's finger.
        /// </summary>
        void OnSuggestion(int index)
        {
            if (index >= m_Suggestions.Count)
                return;
            var s = m_Suggestions[index];
            m_Selected = index;
            m_Working = s.palette.Clone();
            if (m_Set.RemoveAll(c => s.replaced.Contains(c.slot)) > 0)
                SaveSetColors();
            ShowSlotOnWheel();
            RefreshAll();
        }

        void OnSlot(int index)
        {
            m_EditSlot = index;
            ShowSlotOnWheel();
            RefreshAll();
        }

        void ClearSetColors()
        {
            m_Set.Clear();
            SaveSetColors();
            m_Seed = m_Working.dominant;
            m_Selected = -1;
            Recompute();
            RefreshAll();
        }

        void SaveSetColors() => SaveData.SaveWheelColors(m_Set);

        void Recompute() => m_Suggestions = ColorHarmony.Suggest(m_Set, m_Seed);

        /// <summary>The wheel and brightness bar show the color of the slot being edited.</summary>
        void ShowSlotOnWheel() => ColorHarmony.ToRyb(m_Working.Get(Slots[m_EditSlot]), out m_Hue, out m_Sat, out m_Val);

        bool IsSet(PaletteSlot slot) => m_Set.Exists(s => s.slot == slot);

        // ---- visuals -------------------------------------------------------------------------

        void RefreshAll()
        {
            // The wheel texture is drawn at full brightness; tinting the image applies the value.
            m_Wheel.color = new Color(m_Val, m_Val, m_Val, 1f);
            float a = (90f - m_Hue) * Mathf.Deg2Rad;
            m_WheelMarker.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m_Sat * WheelSize * 0.5f;

            for (int x = 0; x < m_StripTexture.width; x++)
                m_StripTexture.SetPixel(x, 0, ColorHarmony.FromRyb(m_Hue, m_Sat, x / (m_StripTexture.width - 1f)));
            m_StripTexture.Apply(false);
            m_StripMarker.anchoredPosition = new Vector2((m_Val - 0.5f) * WheelSize, 0f);

            for (int i = 0; i < m_RowNames.Count; i++)
            {
                var s = m_Suggestions[i];
                m_RowNames[i].Localize(ColorHarmony.DisplayName(s.rule));
                ShowNote(m_RowNotes[i], s);
                for (int k = 0; k < Slots.Length; k++)
                    m_RowSwatches[i][k].color = s.palette.Get(Slots[k]);
                m_RowRings[i].enabled = i == m_Selected;
            }

            for (int k = 0; k < Slots.Length; k++)
            {
                m_SlotSwatches[k].color = m_Working.Get(Slots[k]);
                m_SlotRings[k].enabled = k == m_EditSlot;
                if (IsSet(Slots[k]))
                    m_SlotLabels[k].Localize("{0} · set", Loc.Arg(Slots[k].ToString()));
                else
                    m_SlotLabels[k].Localize(Slots[k].ToString());
                m_SlotLabels[k].fontStyle = IsSet(Slots[k]) ? FontStyles.Bold : FontStyles.Normal;
            }

            var editing = Loc.Arg(Slots[m_EditSlot].ToString());
            if (m_Set.Count == 0)
                m_Hint.Localize("Picking {0}. Tap another swatch to set more colors.", editing);
            else if (m_Set.Count == 1)
                m_Hint.Localize("Picking {0}. Suggestions keep your set color when it fits.", editing);
            else
                m_Hint.Localize("Picking {0}. Suggestions keep all {1} set colors when they fit.", editing, m_Set.Count);
        }

        void ShowNote(TextMeshProUGUI label, ColorHarmony.Suggestion s)
        {
            if (s.replaced.Count > 0)
            {
                var names = new List<string>();
                foreach (var slot in s.replaced)
                    names.Add(slot.ToString());
                label.Localize("Replaces {0}", Loc.Lazy(() => Loc.Join(names)));
            }
            else if (m_Set.Count > 1)
                label.Localize("Keeps your colors");
            else
                label.SetPlain(string.Empty);
        }

        void BuildWheel(Transform parent)
        {
            m_Wheel = new GameObject("Wheel", typeof(RectTransform)).AddComponent<Image>();
            m_Wheel.transform.SetParent(parent, false);
            m_Wheel.sprite = WheelSprite();
            UIKit.Place(m_Wheel.rectTransform, Pad, WheelY, WheelSize, WheelSize);
            m_Wheel.gameObject.AddComponent<ColorPickArea>().Picked += OnWheelPicked;
            m_WheelMarker = Marker(m_Wheel.transform, 18f, 18f);
        }

        void BuildStrip(Transform parent)
        {
            m_StripTexture = new Texture2D(128, 1, TextureFormat.RGBA32, false)
            {
                name = "Brightness Strip",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_Strip = new GameObject("Brightness", typeof(RectTransform)).AddComponent<Image>();
            m_Strip.transform.SetParent(parent, false);
            m_Strip.sprite = Sprite.Create(m_StripTexture, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f), 100f);
            UIKit.Place(m_Strip.rectTransform, Pad, StripY, WheelSize, 28f);
            m_Strip.gameObject.AddComponent<ColorPickArea>().Picked += OnStripPicked;
            m_StripMarker = Marker(m_Strip.transform, 8f, 38f);

            var label = UIKit.Label(parent, "Brightness Label", "Brightness", 11f, UIKit.MutedTextColor);
            UIKit.Place(label.rectTransform, Pad, StripY + 32f, WheelSize, 16f);
        }

        void BuildRow(Transform parent, int index, float y)
        {
            float width = W - RightX - Pad;
            var ring = UIKit.Panel(parent, $"Suggestion {index + 1} Ring", Ring, 10f);
            ring.raycastTarget = false;
            UIKit.Place(ring.rectTransform, RightX - 2f, y - 2f, width + 4f, RowH + 4f);
            m_RowRings.Add(ring);

            // Rows are slots in the sorted suggestion list, so their rule names are filled in on refresh.
            var card = UIKit.Panel(parent, $"Suggestion {index + 1}", Color.white, 8f);
            UIKit.Place(card.rectTransform, RightX, y, width, RowH);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.colors = UIKit.SecondaryColors();
            button.onClick.AddListener(() => OnSuggestion(index));
            card.gameObject.AddComponent<ForgivingClick>();

            var name = UIKit.Label(card.transform, "Name", string.Empty, 11f, UIKit.TextColor, style: FontStyles.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 8f;
            name.fontSizeMax = 11f;
            UIKit.Place(name.rectTransform, 8f, 5f, 122f, 16f);
            m_RowNames.Add(name);

            var note = UIKit.Label(card.transform, "Note", string.Empty, 8.5f, UIKit.MutedTextColor);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            note.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(note.rectTransform, 8f, 22f, 124f, 13f);
            m_RowNotes.Add(note);

            const float swatch = 26f, gap = 4f;
            var swatches = new Image[Slots.Length];
            float sx = width - 8f - Slots.Length * swatch - (Slots.Length - 1) * gap;
            for (int s = 0; s < Slots.Length; s++)
            {
                swatches[s] = UIKit.Edge(UIKit.Panel(card.transform, Slots[s].ToString(), Color.white, 5f));
                swatches[s].raycastTarget = false;
                UIKit.Place(swatches[s].rectTransform, sx + s * (swatch + gap), (RowH - swatch) / 2f, swatch, swatch);
            }
            m_RowSwatches.Add(swatches);
        }

        void BuildSlot(Transform parent, int index)
        {
            float x = Pad + index * (SlotW + SlotGap);
            var ring = UIKit.Panel(parent, $"{Slots[index]} Ring", Ring, 10f);
            ring.raycastTarget = false;
            UIKit.Place(ring.rectTransform, x - 3f, SlotY - 3f, SlotW + 6f, SlotH + 6f);
            m_SlotRings[index] = ring;

            var swatch = UIKit.Edge(UIKit.Panel(parent, Slots[index].ToString(), Color.white, 8f));
            UIKit.Place(swatch.rectTransform, x, SlotY, SlotW, SlotH);
            var button = swatch.gameObject.AddComponent<Button>();
            button.targetGraphic = swatch;
            // Tint transitions would distort the swatch color, so press feedback comes from the ring instead.
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnSlot(index));
            swatch.gameObject.AddComponent<ForgivingClick>();
            m_SlotSwatches[index] = swatch;

            var label = UIKit.Label(parent, $"{Slots[index]} Label", Slots[index].ToString(), 9.5f, UIKit.MutedTextColor,
                TextAlignmentOptions.Top);
            UIKit.Place(label.rectTransform, x - 4f, SlotY + SlotH + 4f, SlotW + 8f, 14f);
            m_SlotLabels[index] = label;
        }

        static RectTransform Marker(Transform parent, float width, float height)
        {
            var outer = UIKit.Panel(parent, "Marker", Ring, Mathf.Min(width, height) / 2f);
            outer.raycastTarget = false;
            var rt = outer.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            var inner = UIKit.Panel(outer.transform, "Inner", Color.white, Mathf.Min(width, height) / 2f - 2f);
            inner.raycastTarget = false;
            UIKit.Stretch(inner.rectTransform, 2.5f);
            return rt;
        }

        static Sprite s_WheelSprite;

        /// <summary>RYB hue around the rim (red at top, clockwise), saturation from centre to edge.</summary>
        static Sprite WheelSprite()
        {
            if (s_WheelSprite != null)
                return s_WheelSprite;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RYB Color Wheel",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half, ny = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float hue = Mathf.Repeat(90f - Mathf.Atan2(ny, nx) * Mathf.Rad2Deg, 360f);
                    Color c = ColorHarmony.FromRyb(hue, Mathf.Clamp01(r), 1f);
                    c.a = Mathf.Clamp01((1f - r) * half + 0.5f); // antialiased rim
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            s_WheelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_WheelSprite;
        }
    }
}
