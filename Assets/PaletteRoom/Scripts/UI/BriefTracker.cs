using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The active brief's checklist: each goal ticks off live as the room changes, with the stars it's
    /// worth so far, a "Reveal & save" button for the finish moment, and a way out of the brief.
    /// </summary>
    public class BriefTracker : MonoBehaviour
    {
        const float W = 330f, Pad = 14f, RowH = 18f, ButtonH = 30f;
        const int MaxRows = 7;

        static readonly Color Done = new Color(0.30f, 0.62f, 0.38f);
        static readonly Color Todo = new Color(0.55f, 0.55f, 0.57f);

        RoomFurniture m_Room;
        PaletteManager m_Palettes;
        DesignBrief m_Brief;
        TextMeshProUGUI m_Title, m_Pitch;
        Image[] m_Stars;
        readonly List<(TextMeshProUGUI mark, TextMeshProUGUI text)> m_Rows = new List<(TextMeshProUGUI, TextMeshProUGUI)>();
        float m_NextCheck;

        public DesignBrief Brief => m_Brief;
        public int Stars { get; private set; }

        public event Action RevealRequested;
        public event Action ExitRequested;

        public void Build(Camera eventCamera, RoomFurniture room, PaletteManager palettes)
        {
            m_Room = room;
            m_Palettes = palettes;
            float rowsY = Pad + 22f + 30f + 24f;
            float height = rowsY + MaxRows * RowH + 8f + ButtonH + Pad;
            var canvas = UIKit.CreateWorldCanvas("Brief Tracker Canvas", new Vector2(W, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 14f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            m_Title = UIKit.Label(p, "Title", string.Empty, 13f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad, W - 2 * Pad - 70f, 20f);
            m_Stars = UIKit.StarRow(p, W - Pad - 64f, Pad, 20f);
            m_Pitch = UIKit.Label(p, "Pitch", string.Empty, 9f, UIKit.MutedTextColor);
            UIKit.Place(m_Pitch.rectTransform, Pad, Pad + 22f, W - 2 * Pad, 28f);
            var goalsHeader = UIKit.Label(p, "Goals", "Goals", 10f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(goalsHeader.rectTransform, Pad, Pad + 52f, W - 2 * Pad, 16f);

            for (int i = 0; i < MaxRows; i++)
            {
                var mark = UIKit.Label(p, $"Mark {i}", string.Empty, 11f, Todo, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                UIKit.Place(mark.rectTransform, Pad, rowsY + i * RowH, 16f, RowH);
                var text = UIKit.Label(p, $"Goal {i}", string.Empty, 10f, UIKit.TextColor);
                UIKit.Place(text.rectTransform, Pad + 18f, rowsY + i * RowH, W - 2 * Pad - 18f, RowH);
                m_Rows.Add((mark, text));
            }

            float buttonsY = height - Pad - ButtonH;
            float half = (W - 2 * Pad - 8f) / 2f;
            var reveal = UIKit.Button(p, "Reveal", "Reveal & save", 11f, true, () => RevealRequested?.Invoke());
            UIKit.Place((RectTransform)reveal.transform, Pad, buttonsY, half, ButtonH);
            var exit = UIKit.Button(p, "Exit", "End brief", 10f, false, () => ExitRequested?.Invoke());
            UIKit.Place((RectTransform)exit.transform, Pad + half + 8f, buttonsY, half, ButtonH);

            m_Room.Changed += Refresh;
            m_Palettes.PaletteChanged += _ => Refresh();
            gameObject.SetActive(false);
        }

        public void Begin(DesignBrief brief)
        {
            m_Brief = brief;
            m_Title.text = brief.title;
            m_Pitch.text = brief.pitch;
            gameObject.SetActive(true);
            Refresh();
        }

        public void End()
        {
            m_Brief = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            // Pieces move without the room changing (dragging), so overlaps are re-checked now and then.
            if (m_Brief != null && Time.unscaledTime >= m_NextCheck)
            {
                m_NextCheck = Time.unscaledTime + 0.5f;
                Refresh();
            }
        }

        void Refresh()
        {
            if (m_Brief == null)
                return;
            var score = m_Brief.Evaluate(m_Room.Items, m_Palettes.Current, m_Room.IsWellPlaced);
            Stars = score.stars;
            UIKit.SetStars(m_Stars, score.stars);
            for (int i = 0; i < m_Rows.Count; i++)
            {
                var (mark, text) = m_Rows[i];
                bool has = i < score.goals.Length;
                mark.gameObject.SetActive(has);
                text.gameObject.SetActive(has);
                if (!has)
                    continue;
                var goal = score.goals[i];
                mark.text = goal.met ? "√" : "○";
                mark.color = goal.met ? Done : Todo;
                text.text = goal.text;
                text.color = goal.met ? UIKit.TextColor : UIKit.MutedTextColor;
            }
        }
    }
}
