using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>Something the user does that a tutorial step can wait for.</summary>
    public enum TutorialGoal
    {
        None,
        MovePanel,
        PickPalette,
        MoveFurniture,
        ClickFurniture,
        SavePalette,
        AddPiece,
    }

    /// <summary>
    /// The guided tour, written for someone in the headset using their hands (with controller notes):
    /// one step at a time on a floating card, with Back / Skip tour / Next. Steps that teach an action
    /// wait for the user to do it (the app reports it through <see cref="Notify"/>), say so, and move on
    /// by themselves; Next is always there so nobody gets stuck. It's started from the welcome panel's
    /// the first sample room or the Palettes panel's Tutorial button.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        const float W = 430f, H = 300f, Pad = 20f, ButtonH = 38f, Gap = 8f;
        const float AdvanceDelay = 1.6f;

        static readonly Color TryColor = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly Color DoneColor = new Color(0.27f, 0.6f, 0.36f);

        struct Step
        {
            public string title, body, tryIt;
            public TutorialGoal goal;

            public Step(string title, string body, string tryIt = null, TutorialGoal goal = TutorialGoal.None)
            {
                this.title = title;
                this.body = body;
                this.tryIt = tryIt;
                this.goal = goal;
            }
        }

        static readonly Step[] Steps =
        {
            new Step("Welcome to Palette Room",
                "This tour shows you how to restyle a room in color, using the furniture in front of you. It takes a couple of minutes. Go back or skip the tour at any time."),
            new Step("Point and pinch",
                "Hold up a hand, palm facing away, and a ray comes out of it. Point the ray at a button and pinch your thumb and index finger together to press it. Up close, poke buttons with a fingertip. With controllers, point and pull the trigger.",
                "Point at Next and pinch to carry on."),
            new Step("Move a panel",
                "Panels stay where you put them in your room instead of following your head. Each one has a bar underneath it, like a window's title bar.",
                "Pinch the bar under the Palettes panel, drag it somewhere comfortable, and let go.", TutorialGoal.MovePanel),
            new Step("Pick a palette",
                "Each card is a five-color palette built on a color-wheel harmony. The room follows the 60-30-10 design rule: about 60% main color, 30% supporting color and 10% accent. The chips along the top filter by harmony.",
                "Pinch any palette card and watch the color sweep across the room.", TutorialGoal.PickPalette),
            new Step("Move the furniture",
                "Every piece can be picked up and put somewhere new. Point at it and pinch from a distance, or reach out and grab it up close. Pieces stay on your floor, and glow red where they won't fit against a wall or your real furniture.",
                "Pinch a piece, drag it to a new spot, and let go.", TutorialGoal.MoveFurniture),
            new Step("Color one piece",
                "A quick pinch and release on a piece, without moving it, opens its colors. Choose which palette color each part uses, such as a sofa's cushions or a table's legs.",
                "Quick-pinch a piece to open its colors.", TutorialGoal.ClickFurniture),
            new Step("Make your own palette",
                "The color wheel turns a color you love into a full palette. Pick a color on the wheel, choose one of the suggested harmonies, tap a swatch to fine-tune it, then press Done.",
                "Open Color wheel in the Palettes panel and save a palette with Done.", TutorialGoal.SavePalette),
            new Step("Add furniture",
                "The shopping list has every piece in real-world sizes. Pinch a picture to set the piece down in front of you, or pinch and hold a picture to pick the piece up and carry it into place.",
                "Open Shopping list in the Palettes panel and add a piece.", TutorialGoal.AddPiece),
            new Step("Design briefs",
                "Want a challenge? Design briefs give you a goal to design to, with the pieces and colors it calls for. Meet the brief to earn up to three stars, then reveal the room to save it."),
            new Step("Your lookbook",
                "In the Lookbook, Reveal & save takes a snapshot of your room. Pinch any saved room to lay it out again, or send its shopping list, with every size and color, to your phone or email."),
            new Step("Saved for next time",
                "Your palettes, color wheel colors, lookbook and the room you're working on are kept on the headset. Next time, choose Continue last room on the welcome panel."),
            new Step("Getting around",
                "Lost the Palettes panel? Press the menu button on a controller to bring it back in front of you. When you're finished, choose Exit in the Palettes panel. You'll be asked to confirm before Palette Room closes."),
            new Step("You're all set",
                "That's the tour. Choose Tutorial in the Palettes panel to take it again any time. Have fun restyling your room!"),
        };

        TextMeshProUGUI m_Counter, m_Title, m_Body, m_Try, m_Status;
        Button m_Back, m_Skip, m_Next;
        TextMeshProUGUI m_BackLabel, m_NextLabel;
        int m_Index = -1;
        bool m_GoalMet;
        Coroutine m_Advance;

        public bool Running => m_Index >= 0;

        /// <summary>The tour ended, finished or skipped.</summary>
        public event Action Ended;

        public void Build(Camera eventCamera)
        {
            var canvas = UIKit.CreateWorldCanvas("Tutorial Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 18f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            m_Counter = UIKit.Label(p, "Counter", string.Empty, 10f, UIKit.MutedTextColor);
            UIKit.Place(m_Counter.rectTransform, Pad, Pad, W - 2 * Pad, 14f);
            m_Title = UIKit.Label(p, "Title", string.Empty, 18f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad + 18f, W - 2 * Pad, 24f);
            m_Body = UIKit.Label(p, "Body", string.Empty, 11.5f, UIKit.MutedTextColor);
            UIKit.Place(m_Body.rectTransform, Pad, Pad + 48f, W - 2 * Pad, 110f);
            m_Try = UIKit.Label(p, "Try It", string.Empty, 11.5f, TryColor, style: FontStyles.Bold);
            UIKit.Place(m_Try.rectTransform, Pad, Pad + 162f, W - 2 * Pad, 32f);
            m_Status = UIKit.Label(p, "Status", string.Empty, 11f, DoneColor, style: FontStyles.Bold);
            UIKit.Place(m_Status.rectTransform, Pad, Pad + 196f, W - 2 * Pad, 16f);

            float third = (W - 2 * Pad - 2 * Gap) / 3f;
            float y = H - Pad - ButtonH;
            m_Back = UIKit.Button(p, "Back", "Back", 13f, false, OnBack);
            UIKit.Place((RectTransform)m_Back.transform, Pad, y, third, ButtonH);
            m_Skip = UIKit.Button(p, "Skip", "Skip tour", 13f, false, OnSkip);
            UIKit.Place((RectTransform)m_Skip.transform, Pad + third + Gap, y, third, ButtonH);
            m_Next = UIKit.Button(p, "Next", "Next", 13f, true, OnNext);
            UIKit.Place((RectTransform)m_Next.transform, Pad + 2 * (third + Gap), y, third, ButtonH);
            m_BackLabel = m_Back.GetComponentInChildren<TextMeshProUGUI>();
            m_NextLabel = m_Next.GetComponentInChildren<TextMeshProUGUI>();
            gameObject.SetActive(false);
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            ShowStep(0);
            Debug.Log("[PaletteRoom] Tutorial started.");
        }

        /// <summary>The user did something; if the current step was waiting for it, the step is done.</summary>
        public void Notify(TutorialGoal goal)
        {
            if (!Running || m_GoalMet || goal == TutorialGoal.None || Steps[m_Index].goal != goal)
                return;
            if (!isActiveAndEnabled)
                return; // put away for the reveal; the step waits until the card is back
            m_GoalMet = true;
            m_Status.text = m_Index + 1 < Steps.Length ? "Nice, that's it! Moving on…" : "Nice, that's it!";
            m_Try.color = DoneColor;
            StopAdvance();
            m_Advance = StartCoroutine(AdvanceSoon(m_Index));
        }

        public void End(bool completed)
        {
            StopAdvance();
            bool wasRunning = Running;
            m_Index = -1;
            SaveData.TutorialSeen = true;
            gameObject.SetActive(false);
            if (wasRunning)
            {
                Debug.Log(completed ? "[PaletteRoom] Tutorial finished." : "[PaletteRoom] Tutorial skipped.");
                Ended?.Invoke();
            }
        }

        void ShowStep(int index)
        {
            StopAdvance();
            m_Index = Mathf.Clamp(index, 0, Steps.Length - 1);
            m_GoalMet = false;
            var step = Steps[m_Index];
            m_Counter.text = $"Tutorial · Step {m_Index + 1} of {Steps.Length}";
            m_Title.text = step.title;
            m_Body.text = step.body;
            m_Try.text = string.IsNullOrEmpty(step.tryIt) ? string.Empty : "Try it: " + step.tryIt;
            m_Try.color = TryColor;
            m_Try.fontStyle = FontStyles.Bold;
            m_Status.text = string.Empty;
            m_Back.interactable = m_Index > 0;
            m_BackLabel.color = m_Index > 0 ? UIKit.TextColor : UIKit.MutedTextColor;
            bool last = m_Index == Steps.Length - 1;
            m_NextLabel.text = last ? "Finish" : "Next";
            m_Skip.gameObject.SetActive(!last);
        }

        IEnumerator AdvanceSoon(int from)
        {
            yield return new WaitForSecondsRealtime(AdvanceDelay);
            m_Advance = null;
            if (m_Index == from)
                ShowStep(from + 1);
        }

        void StopAdvance()
        {
            if (m_Advance != null)
                StopCoroutine(m_Advance);
            m_Advance = null;
        }

        void OnNext()
        {
            if (m_Index >= Steps.Length - 1)
                End(completed: true);
            else
                ShowStep(m_Index + 1);
        }

        void OnBack()
        {
            if (Running && m_Index > 0)
                ShowStep(m_Index - 1);
        }

        void OnSkip() => End(completed: false);
    }
}
