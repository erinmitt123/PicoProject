using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// A short notice at the top of the view after the room color scan: which palette was chosen, its
    /// harmony and colors, and the room colors it was matched to. Dismissed with OK or after a while.
    /// </summary>
    public class RoomMatchBanner : MonoBehaviour
    {
        const float W = 520f, H = 150f, Pad = 18f, Swatch = 30f, Gap = 6f;
        const float ShowSeconds = 25f;

        Transform m_Head;
        TextMeshProUGUI m_Name, m_Details;
        Image[] m_PaletteSwatches, m_RoomSwatches;
        Coroutine m_Hide;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;
            var canvas = UIKit.CreateWorldCanvas("Room Match Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 16f);
            UIKit.Stretch(panel.rectTransform);
            var body = panel.transform;

            var title = UIKit.Label(body, "Title", "Using palette that matches colors in this room:", 12f, UIKit.MutedTextColor);
            UIKit.Place(title.rectTransform, Pad, Pad, W - 2 * Pad - 70f, 16f);
            m_Name = UIKit.Label(body, "Name", string.Empty, 18f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Name.rectTransform, Pad, Pad + 18f, W - 2 * Pad - 70f, 24f);

            var ok = UIKit.Button(body, "OK", "OK", 12f, true, Hide);
            UIKit.Place((RectTransform)ok.transform, W - Pad - 60f, Pad, 60f, 30f);

            // Palette swatches on the left, the room's colors (sized by how much they cover) on the right.
            float rowY = Pad + 50f;
            m_PaletteSwatches = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                m_PaletteSwatches[i] = UIKit.Panel(body, $"Palette {i}", Color.white, 6f);
                UIKit.Place(m_PaletteSwatches[i].rectTransform, Pad + i * (Swatch + Gap), rowY, Swatch, Swatch);
            }
            var roomLabel = UIKit.Label(body, "Room Label", "Room", 10f, UIKit.MutedTextColor, TextAlignmentOptions.MidlineRight);
            UIKit.Place(roomLabel.rectTransform, W / 2f - 10f, rowY, 50f, Swatch);
            m_RoomSwatches = new Image[6];
            for (int i = 0; i < m_RoomSwatches.Length; i++)
            {
                m_RoomSwatches[i] = UIKit.Panel(body, $"Room {i}", Color.white, 6f);
                UIKit.Place(m_RoomSwatches[i].rectTransform, W / 2f + 46f + i * (Swatch + Gap), rowY, Swatch, Swatch);
            }

            m_Details = UIKit.Label(body, "Details", string.Empty, 10f, UIKit.MutedTextColor);
            UIKit.Place(m_Details.rectTransform, Pad, rowY + Swatch + 8f, W - 2 * Pad, 28f);
            gameObject.SetActive(false);
        }

        public void Show(RoomColorScanner.Match match)
        {
            var p = match.palette;
            m_Name.text = p.name;
            var colors = new[] { p.dominant, p.secondary, p.accent, p.neutral, p.highlight };
            for (int i = 0; i < 5; i++)
                m_PaletteSwatches[i].color = colors[i];
            for (int i = 0; i < m_RoomSwatches.Length; i++)
            {
                bool has = i < match.roomColors.Length;
                m_RoomSwatches[i].gameObject.SetActive(has);
                if (has)
                    m_RoomSwatches[i].color = match.roomColors[i].color;
            }
            string main = match.roomColors.Length > 0 ? Describe(match.roomColors[0].color) : "neutral";
            m_Details.text = $"{ColorHarmony.DisplayName(p.harmony)} palette · the room reads mostly {main} " +
                             $"({match.roomColors[0].share:P0}), so this is the closest of {PalettePresets.All.Count} palettes. Pick another anytime from the dock.";

            gameObject.SetActive(true);
            // Top of the view, above the menus.
            UIKit.PlaceInFront(transform, m_Head, 1.15f, -0.3f);
            if (m_Hide != null)
                StopCoroutine(m_Hide);
            m_Hide = StartCoroutine(HideLater());
        }

        /// <summary>The camera couldn't be read (e.g. the PICO Emulator has no camera access): say so and show the palette in use.</summary>
        public void ShowUnavailable(ColorPalette current)
        {
            m_Name.text = current.name;
            var colors = new[] { current.dominant, current.secondary, current.accent, current.neutral, current.highlight };
            for (int i = 0; i < 5; i++)
                m_PaletteSwatches[i].color = colors[i];
            foreach (var s in m_RoomSwatches)
                s.gameObject.SetActive(false);
            m_Details.text = "Couldn't read this room's colors here (the headset camera isn't available), so this is the default palette. Pick another anytime from the dock.";
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, m_Head, 1.15f, -0.3f);
            if (m_Hide != null)
                StopCoroutine(m_Hide);
            m_Hide = StartCoroutine(HideLater());
        }

        public void Hide() => gameObject.SetActive(false);

        IEnumerator HideLater()
        {
            yield return new WaitForSeconds(ShowSeconds);
            Hide();
        }

        /// <summary>A plain-words name for a color (e.g. "warm white", "dark brown").</summary>
        static string Describe(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            string tone = v < 0.25f ? "dark " : v > 0.85f && s < 0.15f ? "" : v > 0.7f ? "light " : "";
            if (s < 0.12f)
                return v > 0.85f ? "white" : v < 0.25f ? "black" : tone + "grey";
            float deg = h * 360f;
            string hue = deg < 15f || deg >= 345f ? "red" : deg < 40f ? (v < 0.6f ? "brown" : "orange") : deg < 65f ? (s < 0.35f ? "beige" : "yellow")
                : deg < 160f ? "green" : deg < 200f ? "teal" : deg < 255f ? "blue" : deg < 290f ? "purple" : "pink";
            if (s < 0.3f && (hue == "orange" || hue == "yellow" || hue == "brown"))
                hue = v > 0.6f ? "beige" : "taupe";
            return tone + hue;
        }
    }
}
