using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The lookbook: past rooms, newest first, each with its snapshot, title and stars, palette and date.
    /// Tap a room to lay it out again in front of the user, or send its shopping list (pieces, sizes,
    /// colors) to a phone or email. The current room can be revealed and saved
    /// from here too, without a brief.
    /// </summary>
    public class LookbookPanel : MonoBehaviour
    {
        const float W = 500f, Pad = 16f, HeaderH = 26f, CardW = 226f, CardH = 214f, Gap = 10f, ButtonH = 30f;

        RectTransform m_Grid;
        TextMeshProUGUI m_Empty;
        readonly List<Texture2D> m_Thumbnails = new List<Texture2D>();

        public event Action<Lookbook.Entry> RestoreRequested;
        /// <summary>Send this room's shopping list; the card is passed so the send panel can open beside it.</summary>
        public event Action<Lookbook.Entry, Transform> SendRequested;
        public event Action RevealCurrentRequested;
        public event Action Closed;

        public void Build(Camera eventCamera)
        {
            float gridH = 1.6f * (CardH + Gap);
            float height = Pad + HeaderH + Gap + ButtonH + Gap + gridH + Gap + ButtonH + Pad;
            var canvas = UIKit.CreateWorldCanvas("Lookbook Canvas", new Vector2(W, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 16f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var header = UIKit.Label(p, "Header", "Lookbook", 15f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(header.rectTransform, Pad, Pad, W - 2 * Pad, HeaderH);
            float y = Pad + HeaderH + Gap;
            var reveal = UIKit.Button(p, "Reveal Current", "Reveal & save this room", 11f, true, () => RevealCurrentRequested?.Invoke());
            UIKit.Place((RectTransform)reveal.transform, Pad, y, W - 2 * Pad, ButtonH);
            y += ButtonH + Gap;

            m_Grid = UIKit.ScrollGrid(p, "Lookbook Grid", Pad, y, W - 2 * Pad, gridH, new Vector2(CardW, CardH), Gap, 2);
            m_Empty = UIKit.Label(p, "Empty", "No rooms yet. Finish a design brief, or reveal the room you're in, to start your lookbook.",
                11f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(m_Empty.rectTransform, Pad + 20f, y + 40f, W - 2 * Pad - 40f, 40f);
            y += gridH + Gap;

            var close = UIKit.Button(p, "Close", "Close", 11f, false, () => { gameObject.SetActive(false); Closed?.Invoke(); });
            UIKit.Place((RectTransform)close.transform, Pad, y, W - 2 * Pad, ButtonH);

            Lookbook.Changed += Rebuild;
            Rebuild();
        }

        void OnDestroy()
        {
            Lookbook.Changed -= Rebuild;
            ClearThumbnails();
        }

        public void Open(Transform head)
        {
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, head, 0.9f, 0.15f);
        }

        void ClearThumbnails()
        {
            foreach (var t in m_Thumbnails)
                if (t != null)
                    Destroy(t);
            m_Thumbnails.Clear();
        }

        void Rebuild()
        {
            if (m_Grid == null)
                return;
            foreach (Transform child in m_Grid)
                Destroy(child.gameObject);
            ClearThumbnails();
            var entries = Lookbook.Entries;
            m_Empty.gameObject.SetActive(entries.Count == 0);
            foreach (var entry in entries)
                AddCard(entry);
        }

        void AddCard(Lookbook.Entry entry)
        {
            var card = UIKit.Panel(m_Grid, entry.title, new Color(1f, 1f, 1f, 0.75f), 10f);
            // Tap anywhere on the card to load the room (the Send list button handles its own taps).
            var open = card.gameObject.AddComponent<Button>();
            open.targetGraphic = card;
            open.colors = UIKit.SecondaryColors();
            open.onClick.AddListener(() => RestoreRequested?.Invoke(entry));
            card.gameObject.AddComponent<ForgivingClick>();
            var c = card.transform;

            var photo = new GameObject("Snapshot", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            photo.transform.SetParent(c, false);
            UIKit.Place(photo.rectTransform, 6f, 6f, CardW - 12f, (CardW - 12f) * 0.625f);
            photo.raycastTarget = false;
            var tex = Lookbook.LoadSnapshot(entry);
            if (tex != null)
            {
                photo.texture = tex;
                m_Thumbnails.Add(tex);
            }
            else
            {
                photo.color = new Color(0.9f, 0.88f, 0.85f);
            }

            float y = 6f + (CardW - 12f) * 0.625f + 6f;
            var title = UIKit.Label(c, "Title", entry.title, 12f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, 8f, y, CardW - 80f, 16f);
            if (!string.IsNullOrEmpty(entry.briefId))
            {
                UIKit.SetStars(UIKit.StarRow(c, CardW - 62f, y, 16f), entry.stars);
            }
            y += 18f;
            var meta = UIKit.Label(c, "Meta", $"{entry.paletteName} · {entry.pieces.Count} pieces · {entry.date}", 9f, UIKit.MutedTextColor);
            UIKit.Place(meta.rectTransform, 8f, y, CardW - 16f, 14f);
            y += 16f;
            if (entry.paletteHex != null)
            {
                for (int i = 0; i < entry.paletteHex.Length; i++)
                {
                    ColorUtility.TryParseHtmlString(entry.paletteHex[i], out var color);
                    var sw = UIKit.Panel(c, $"Swatch {i}", color, 4f);
                    UIKit.Place(sw.rectTransform, 8f + i * 20f, y, 16f, 16f);
                }
            }
            var send = UIKit.Button(c, "Send List", "Send list", 9.5f, false, () => SendRequested?.Invoke(entry, card.transform));
            UIKit.Place((RectTransform)send.transform, CardW - 84f, y - 4f, 76f, 24f);
        }
    }
}
