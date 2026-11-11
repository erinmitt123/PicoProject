using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// A notice at the top of the view for the start-up room color scan: a spinner with "Matching to
    /// your room…" while the room is read, then the chosen palette (name, harmony, colors) beside the
    /// room colors it was matched to. Dismissed with OK or after a while.
    /// </summary>
    public class RoomMatchBanner : MonoBehaviour
    {
        const float W = 520f, H = 150f, Pad = 18f, Swatch = 30f, Gap = 6f;
        const float ShowSeconds = 25f;
        const float MinScanSeconds = 1.6f; // long enough to read the spinner, so the change doesn't feel abrupt

        Transform m_Head;
        TextMeshProUGUI m_Title, m_Name, m_Details, m_RoomLabel;
        Image[] m_PaletteSwatches, m_RoomSwatches;
        Image m_Spinner;
        Button m_Ok;
        Coroutine m_Hide, m_Reveal;
        float m_ScanStarted;
        bool m_Scanning;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;
            var canvas = UIKit.CreateWorldCanvas("Room Match Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 16f);
            UIKit.Stretch(panel.rectTransform);
            var body = panel.transform;

            m_Title = UIKit.Label(body, "Title", string.Empty, 12f, UIKit.MutedTextColor);
            UIKit.Place(m_Title.rectTransform, Pad, Pad, W - 2 * Pad - 70f, 16f);
            m_Name = UIKit.Label(body, "Name", string.Empty, 18f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Name.rectTransform, Pad, Pad + 18f, W - 2 * Pad - 70f, 24f);

            m_Ok = UIKit.Button(body, "OK", "OK", 12f, true, Hide);
            UIKit.Place((RectTransform)m_Ok.transform, W - Pad - 60f, Pad, 60f, 30f);

            // Spinner: a three-quarter ring that turns while the room is being read.
            m_Spinner = UIKit.Panel(body, "Spinner", UIKit.PrimaryColor, 0f);
            m_Spinner.sprite = null;
            m_Spinner.type = Image.Type.Filled;
            m_Spinner.fillMethod = Image.FillMethod.Radial360;
            m_Spinner.fillAmount = 0.75f;
            UIKit.Place(m_Spinner.rectTransform, W - Pad - 44f, Pad + 4f, 36f, 36f);
            var hole = UIKit.Panel(m_Spinner.transform, "Hole", UIKit.PanelColor, 13f);
            UIKit.Stretch(hole.rectTransform, 6f);

            // Palette swatches on the left, the room's colors on the right.
            float rowY = Pad + 50f;
            m_PaletteSwatches = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                m_PaletteSwatches[i] = UIKit.Panel(body, $"Palette {i}", Color.white, 6f);
                UIKit.Place(m_PaletteSwatches[i].rectTransform, Pad + i * (Swatch + Gap), rowY, Swatch, Swatch);
            }
            m_RoomLabel = UIKit.Label(body, "Room Label", "Room", 10f, UIKit.MutedTextColor, TextAlignmentOptions.MidlineRight);
            UIKit.Place(m_RoomLabel.rectTransform, W / 2f - 10f, rowY, 50f, Swatch);
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

        /// <summary>Shows the spinner while the room's colors are read.</summary>
        public void ShowScanning()
        {
            m_Scanning = true;
            m_ScanStarted = Time.unscaledTime;
            m_Title.Localize("One moment…");
            m_Name.Localize("Matching to your room…");
            m_Details.Localize("Reading the colors around you to pick the palette that suits this room.");
            SetResultVisible(false);
            Open();
        }

        /// <summary>
        /// Reveals the match (after the spinner has shown for a moment) and calls <paramref name="onShown"/>
        /// as it appears — the moment to restyle the room, so the colors change with the message.
        /// </summary>
        public void Show(RoomColorScanner.Match match, Action onShown = null)
        {
            if (m_Reveal != null)
                StopCoroutine(m_Reveal);
            if (!gameObject.activeSelf)
                Open();
            m_Reveal = StartCoroutine(Reveal(match, onShown));
        }

        IEnumerator Reveal(RoomColorScanner.Match match, Action onShown)
        {
            if (m_Scanning)
            {
                float wait = MinScanSeconds - (Time.unscaledTime - m_ScanStarted);
                if (wait > 0f)
                    yield return new WaitForSecondsRealtime(wait);
            }
            m_Scanning = false;

            var p = match.palette;
            m_Title.Localize("Using palette that matches colors in this room:");
            m_Name.Localize(p.name);
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
            m_RoomLabel.gameObject.SetActive(true);
            string main = match.roomColors.Length > 0 ? ColorNames.Describe(match.roomColors[0].color) : "neutral";
            string mainName = main;
            float share = match.roomColors.Length > 0 ? match.roomColors[0].share : 1f;
            string source = match.fromEmulatorScene ? "The emulator's room" : "The room";
            m_Details.Localize("{0} palette · {1} reads mostly {2} ({3}), so this is the closest of {4} palettes. Pick another anytime from the dock.",
                Loc.Arg(ColorHarmony.DisplayName(p.harmony)), Loc.Arg(source), Loc.Lazy(() => Loc.ColorName(mainName)), share.ToString("P0"), PalettePresets.All.Count);
            SetResultVisible(true);
            onShown?.Invoke();
            RestartHideTimer();
        }

        /// <summary>The camera couldn't be read: say so and show the palette in use.</summary>
        public void ShowUnavailable(ColorPalette current)
        {
            m_Scanning = false;
            m_Title.Localize("Couldn't match this room's colors");
            m_Name.Localize(current.name);
            var colors = new[] { current.dominant, current.secondary, current.accent, current.neutral, current.highlight };
            for (int i = 0; i < 5; i++)
                m_PaletteSwatches[i].color = colors[i];
            SetResultVisible(true);
            foreach (var s in m_RoomSwatches)
                s.gameObject.SetActive(false);
            m_RoomLabel.gameObject.SetActive(false);
            m_Details.Localize("The headset camera isn't available here, so this is the default palette. Pick another anytime from the dock.");
            if (!gameObject.activeSelf)
                Open();
            RestartHideTimer();
        }

        public void Hide() => gameObject.SetActive(false);

        void Open()
        {
            gameObject.SetActive(true);
            // Top of the view, above the menus.
            UIKit.PlaceInFront(transform, m_Head, 1.15f, -0.3f);
        }

        void SetResultVisible(bool visible)
        {
            m_Spinner.gameObject.SetActive(!visible);
            m_Ok.gameObject.SetActive(visible);
            foreach (var s in m_PaletteSwatches)
                s.gameObject.SetActive(visible);
            foreach (var s in m_RoomSwatches)
                s.gameObject.SetActive(visible);
            m_RoomLabel.gameObject.SetActive(visible);
        }

        void RestartHideTimer()
        {
            if (m_Hide != null)
                StopCoroutine(m_Hide);
            m_Hide = StartCoroutine(HideLater());
        }

        void Update()
        {
            if (m_Spinner != null && m_Spinner.gameObject.activeInHierarchy)
                m_Spinner.rectTransform.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
        }

        IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(ShowSeconds);
            Hide();
        }

    }
}
