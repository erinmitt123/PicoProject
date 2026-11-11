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
        OpenShoppingList,
        CloseShoppingList,
    }

    /// <summary>
    /// The guided tour. It furnishes an empty room one piece at a time and then walks through every
    /// screen, with a <see cref="TutorialPointer"/> outlining the exact button to press (or hovering over
    /// the piece to use) at each step, and a spoken English narration. Steps that teach an action wait for
    /// the user to do it (the app reports it through <see cref="Notify"/>), cheer, and move on by
    /// themselves; Next is always there so nobody gets stuck.
    /// The steps live in Resources/Tutorial/tutorial.json (also read by tools/generate_narration.py, which
    /// makes Resources/Tutorial/Voice/&lt;id&gt;.ogg); targets are named there and found by the app.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        const float W = 440f, H = 316f, Pad = 20f, ButtonH = 38f, Gap = 8f;
        const float AdvanceDelay = 1.4f;

        static readonly Color TryColor = new Color(0.17f, 0.20f, 0.24f, 1f);
        static readonly Color DoneColor = new Color(0.27f, 0.6f, 0.36f);

        [Serializable]
        class Step
        {
            public string id, title, body, tryIt, say, goal, detail, target;
            [NonSerialized] public TutorialGoal goalValue;
        }

        [Serializable]
        class StepList
        {
            public Step[] steps;
        }

        Step[] m_Steps;
        TextMeshProUGUI m_Counter, m_Title, m_Body, m_Try, m_Status;
        Button m_Back, m_Skip, m_Next, m_Replay;
        TextMeshProUGUI m_BackLabel, m_NextLabel;
        RectTransform m_ProgressFill;
        TutorialPointer m_Pointer;
        Func<string, Component> m_ResolveTarget;
        AudioSource m_Voice;
        int m_Index = -1;
        bool m_GoalMet;
        Coroutine m_Advance;

        public bool Running => m_Index >= 0;

        /// <summary>The Next button, which the tour points at while teaching how to press.</summary>
        public RectTransform NextButton => m_Next != null ? (RectTransform)m_Next.transform : null;

        /// <summary>The tour ended, finished or skipped.</summary>
        public event Action Ended;

        /// <summary>A step was shown; the argument is its target name (empty for none), so the app can make it reachable.</summary>
        public event Action<string> StepShown;

        /// <param name="resolveTarget">Finds a step's target by name ("dock.shopping", "item:Sofa"…): a UI RectTransform, a piece, or null when it isn't on show.</param>
        public void Build(Camera eventCamera, TutorialPointer pointer, Func<string, Component> resolveTarget)
        {
            m_Pointer = pointer;
            m_ResolveTarget = resolveTarget;
            m_Steps = LoadSteps();

            var canvas = UIKit.CreateWorldCanvas("Tutorial Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 18f);
            var p = panel.transform;

            m_Counter = UIKit.Label(p, "Counter", string.Empty, 10f, UIKit.MutedTextColor);
            UIKit.Place(m_Counter.rectTransform, Pad, Pad, W - 2 * Pad - 110f, 14f);
            m_Replay = UIKit.Button(p, "Replay", "Hear again", 9.5f, false, PlayVoice);
            UIKit.Place((RectTransform)m_Replay.transform, W - Pad - 100f, Pad - 4f, 100f, 22f);

            var track = UIKit.Panel(p, "Progress", new Color(0f, 0f, 0f, 0.08f), 2f);
            track.raycastTarget = false;
            UIKit.Place(track.rectTransform, Pad, Pad + 22f, W - 2 * Pad, 4f);
            var fill = UIKit.Panel(track.transform, "Fill", UIKit.ThemeColor(2), 2f);
            fill.raycastTarget = false;
            m_ProgressFill = fill.rectTransform;
            m_ProgressFill.anchorMin = Vector2.zero;
            m_ProgressFill.anchorMax = new Vector2(0f, 1f);
            m_ProgressFill.pivot = new Vector2(0f, 0.5f);
            m_ProgressFill.offsetMin = m_ProgressFill.offsetMax = Vector2.zero;

            m_Title = UIKit.Label(p, "Title", string.Empty, 18f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad + 34f, W - 2 * Pad, 24f);
            m_Body = UIKit.Label(p, "Body", string.Empty, 11.5f, UIKit.MutedTextColor);
            UIKit.Place(m_Body.rectTransform, Pad, Pad + 64f, W - 2 * Pad, 104f);
            m_Try = UIKit.Label(p, "Try It", string.Empty, 12f, TryColor, style: FontStyles.Bold);
            UIKit.Place(m_Try.rectTransform, Pad, Pad + 172f, W - 2 * Pad, 32f);
            m_Status = UIKit.Label(p, "Status", string.Empty, 11f, DoneColor, style: FontStyles.Bold);
            UIKit.Place(m_Status.rectTransform, Pad, Pad + 206f, W - 2 * Pad, 16f);

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

            m_Voice = gameObject.AddComponent<AudioSource>();
            m_Voice.playOnAwake = false;
            m_Voice.spatialBlend = 0f;
            m_Voice.priority = 10;

            Loc.Changed += OnLanguageChanged;
            gameObject.SetActive(false);
        }

        void OnDestroy() => Loc.Changed -= OnLanguageChanged;

        static Step[] LoadSteps()
        {
            var asset = Resources.Load<TextAsset>("Tutorial/tutorial");
            Step[] steps = null;
            if (asset != null)
            {
                try
                {
                    steps = JsonUtility.FromJson<StepList>(asset.text)?.steps;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PaletteRoom] Couldn't read the tutorial: {e.Message}");
                }
            }
            if (steps == null || steps.Length == 0)
            {
                Debug.LogWarning("[PaletteRoom] Tutorial steps missing; using a short tour.");
                steps = new[]
                {
                    new Step { id = "welcome", title = "Welcome to Palette Room", body = "Pick palettes from the Palettes panel, add furniture from the shopping list, and grab pieces to move them." },
                };
            }
            foreach (var s in steps)
                s.goalValue = Enum.TryParse(s.goal, out TutorialGoal g) ? g : TutorialGoal.None;
            return steps;
        }

        public void Begin()
        {
            gameObject.SetActive(true);
            ShowStep(0);
            Debug.Log("[PaletteRoom] Tutorial started.");
        }

        /// <summary>The user did something; if the current step was waiting for it, the step is done.</summary>
        /// <param name="detail">What it was done to, for steps that ask for one thing (the kind of piece added).</param>
        public void Notify(TutorialGoal goal, string detail = null)
        {
            if (!Running || m_GoalMet || goal == TutorialGoal.None)
                return;
            var step = m_Steps[m_Index];
            if (step.goalValue != goal || (!string.IsNullOrEmpty(step.detail) && step.detail != detail))
                return;
            if (!isActiveAndEnabled)
                return; // put away for the reveal; the step waits until the card is back
            m_GoalMet = true;
            m_Status.Localize(m_Index + 1 < m_Steps.Length ? "Nice, that's it! Moving on…" : "Nice, that's it!");
            m_Try.color = DoneColor;
            m_Pointer?.Point(null);
            Sfx.Play(SfxKind.Success);
            StopAdvance();
            m_Advance = StartCoroutine(AdvanceSoon(m_Index));
        }

        public void End(bool completed)
        {
            StopAdvance();
            bool wasRunning = Running;
            m_Index = -1;
            SaveData.TutorialSeen = true;
            StopVoice();
            m_Pointer?.Point(null);
            gameObject.SetActive(false);
            if (wasRunning)
            {
                if (completed)
                    Sfx.Play(SfxKind.Success);
                Debug.Log(completed ? "[PaletteRoom] Tutorial finished." : "[PaletteRoom] Tutorial skipped.");
                Ended?.Invoke();
            }
        }

        void ShowStep(int index)
        {
            StopAdvance();
            m_Index = Mathf.Clamp(index, 0, m_Steps.Length - 1);
            m_GoalMet = false;
            var step = m_Steps[m_Index];
            m_Counter.Localize("Tutorial · Step {0} of {1}", m_Index + 1, m_Steps.Length);
            m_ProgressFill.anchorMax = new Vector2((m_Index + 1f) / m_Steps.Length, 1f);
            m_ProgressFill.GetComponent<Image>().color = UIKit.ThemeColor(2);
            m_Title.Localize(step.title);
            m_Body.Localize(step.body);
            if (string.IsNullOrEmpty(step.tryIt))
                m_Try.SetPlain(string.Empty);
            else
                m_Try.Localize("Try it: {0}", Loc.Arg(step.tryIt));
            m_Try.color = TryColor;
            m_Status.SetPlain(string.Empty);
            m_Back.interactable = m_Index > 0;
            m_BackLabel.color = m_Index > 0 ? UIKit.TextColor : UIKit.MutedTextColor;
            bool last = m_Index == m_Steps.Length - 1;
            m_NextLabel.Localize(last ? "Finish" : "Next");
            m_Skip.gameObject.SetActive(!last);

            string target = step.target ?? string.Empty;
            StepShown?.Invoke(target);
            if (m_Pointer != null)
            {
                if (string.IsNullOrEmpty(target) || m_ResolveTarget == null)
                    m_Pointer.Point(null);
                else
                    m_Pointer.Point(() => m_ResolveTarget(target));
            }
            PlayVoice();
        }

        // ---- narration -------------------------------------------------------------------------

        AudioClip VoiceClip()
        {
            if (!Running || !Loc.IsEnglish)
                return null; // recorded in English only; Chinese reads from the card
            return Resources.Load<AudioClip>("Tutorial/Voice/" + m_Steps[m_Index].id);
        }

        void PlayVoice()
        {
            var clip = VoiceClip();
            m_Replay.gameObject.SetActive(clip != null);
            if (clip == null)
            {
                StopVoice();
                return;
            }
            m_Voice.Stop();
            m_Voice.clip = clip;
            m_Voice.volume = AudioLevels.VoiceGain;
            m_Voice.Play();
            AmbientMusic.Instance?.Duck(true);
        }

        void StopVoice()
        {
            if (m_Voice != null)
                m_Voice.Stop();
            AmbientMusic.Instance?.Duck(false);
        }

        void Update()
        {
            if (m_Voice == null)
                return;
            m_Voice.volume = AudioLevels.VoiceGain;
            if (!m_Voice.isPlaying)
                AmbientMusic.Instance?.Duck(false);
        }

        void OnDisable() => StopVoice();

        void OnLanguageChanged()
        {
            if (!Running)
                return;
            if (Loc.IsEnglish)
                PlayVoice();
            else
            {
                StopVoice();
                m_Replay.gameObject.SetActive(false);
            }
        }

        // ---- moving through the steps ------------------------------------------------------------

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
            if (m_Index >= m_Steps.Length - 1)
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
