using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The design briefs, one card per brief (easiest first): its pitch, what it asks for, the best
    /// stars earned so far, and a Start button. Starting a brief keeps the room as it is; what is already placed counts.
    /// </summary>
    public class BriefsPanel : MonoBehaviour
    {
        const float W = 470f, Pad = 16f, HeaderH = 26f, CardH = 96f, Gap = 8f, ButtonH = 30f;

        readonly List<(DesignBrief brief, Image[] stars)> m_Cards = new List<(DesignBrief, Image[])>();

        public event Action<DesignBrief> StartRequested;
        public event Action Closed;

        public void Build(Camera eventCamera)
        {
            float listH = 3.3f * (CardH + Gap);
            float height = Pad + HeaderH + 18f + listH + Gap + ButtonH + Pad;
            var canvas = UIKit.CreateWorldCanvas("Briefs Canvas", new Vector2(W, height), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 16f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var header = UIKit.Label(p, "Header", "Design briefs", 15f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(header.rectTransform, Pad, Pad, W - 2 * Pad, HeaderH);
            var sub = UIKit.Label(p, "Sub", "Pick a goal and design to it. Earn up to three stars, then reveal it to your lookbook.", 10f, UIKit.MutedTextColor);
            UIKit.Place(sub.rectTransform, Pad, Pad + HeaderH, W - 2 * Pad, 16f);

            var list = UIKit.ScrollGrid(p, "Brief List", Pad, Pad + HeaderH + 18f, W - 2 * Pad, listH, new Vector2(W - 2 * Pad - 16f, CardH), Gap, 1);
            for (int i = 0; i < BriefCatalog.All.Length; i++)
                AddCard(list, BriefCatalog.All[i], i + 1);

            var close = UIKit.Button(p, "Close", "Close", 11f, false, () => { gameObject.SetActive(false); Closed?.Invoke(); });
            UIKit.Place((RectTransform)close.transform, Pad, height - Pad - ButtonH, W - 2 * Pad, ButtonH);

            Lookbook.Changed += Refresh;
        }

        void OnDestroy() => Lookbook.Changed -= Refresh;

        void AddCard(RectTransform list, DesignBrief brief, int level)
        {
            float cardW = W - 2 * Pad - 16f;
            var card = UIKit.Panel(list, brief.title, new Color(1f, 1f, 1f, 0.7f), 10f);
            var c = card.transform;
            var title = UIKit.Label(c, "Title", string.Empty, 13f, UIKit.TextColor, style: FontStyles.Bold);
            title.Localize("Level {0} · {1}", level, Loc.Arg(brief.title));
            UIKit.Place(title.rectTransform, 10f, 8f, cardW - 110f, 18f);
            var stars = UIKit.StarRow(c, cardW - 70f, 8f, 18f);
            var pitch = UIKit.Label(c, "Pitch", brief.pitch, 9.5f, UIKit.MutedTextColor);
            UIKit.Place(pitch.rectTransform, 10f, 28f, cardW - 110f, 26f);

            string Needs()
            {
                var needs = new List<string>();
                foreach (var (kind, count) in brief.mustHave)
                {
                    string display = FurnitureCatalog.Get(kind).displayName;
                    if (Loc.IsEnglish)
                        needs.Add(count > 1 ? $"{count} {display.ToLowerInvariant()}s" : display.ToLowerInvariant());
                    else
                        needs.Add(count > 1 ? $"{count} × {Loc.T(display)}" : Loc.T(display));
                }
                return string.Join(Loc.IsEnglish ? ", " : "、", needs);
            }
            var goal = UIKit.Label(c, "Goal", string.Empty, 9f, UIKit.TextColor);
            goal.Localize("Needs {0} · {1} · {2} pieces max", Loc.Lazy(Needs),
                Loc.Lazy(() => Loc.IsEnglish ? brief.paletteGoal.ToLowerInvariant() : Loc.T(brief.paletteGoal)), brief.maxPieces);
            UIKit.Place(goal.rectTransform, 10f, 58f, cardW - 110f, 30f);

            var start = UIKit.Button(c, "Start", "Start", 11f, true, () => StartRequested?.Invoke(brief));
            UIKit.Place((RectTransform)start.transform, cardW - 96f, 52f, 86f, 32f);
            m_Cards.Add((brief, stars));
        }

        public void Open(Transform head)
        {
            Refresh();
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, head, 0.9f, 0.15f);
        }

        void Refresh()
        {
            foreach (var (brief, stars) in m_Cards)
            {
                int best = Lookbook.BestStars(brief.id);
                UIKit.SetStars(stars, best);
            }
        }
    }
}
