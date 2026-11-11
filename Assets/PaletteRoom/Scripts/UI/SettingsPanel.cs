using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Settings: sound levels for the music, sound effects, the tutorial voice and everything at once, each
    /// with a slider to point at, pinch and drag and − / + buttons for small steps (easier with hands); the
    /// language (English or 中文); and the ways out: back to the main menu, or quit the app to the PICO home
    /// screen (after a confirmation). Levels apply as they change and are saved when the panel closes.
    /// Opened from the welcome panel and the Palettes dock.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        const float W = 360f, Pad = 16f, HeaderH = 26f, RowGap = 12f, LabelH = 16f, SliderH = 28f, StepW = 34f, ButtonH = 30f;
        const float Step = 0.1f;

        [SerializeField] float distance = 0.75f;
        [SerializeField] float belowEyes = 0.12f;

        class Row
        {
            public Func<float> get;
            public Action<float> set;
            public Slider slider;
            public TextMeshProUGUI value;
        }

        readonly Row[] m_Rows = new Row[4];
        Transform m_Head;
        bool m_Syncing;
        Button m_English, m_Chinese, m_MainMenu, m_Gaze, m_Rays;

        public event Action Closed;
        /// <summary>Back to the welcome panel (the room in progress is kept).</summary>
        public event Action MainMenuRequested;
        /// <summary>Leave the app for the PICO home screen.</summary>
        public event Action QuitRequested;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;
            AudioLevels.Ensure();

            float rowH = LabelH + 4f + SliderH;
            float rowsY = Pad + HeaderH + 8f;
            float languageY = rowsY + 4 * (rowH + RowGap);
            float inputY = languageY + LabelH + 4f + ButtonH + RowGap;
            float buttonsY = inputY + LabelH + 4f + ButtonH + RowGap + 4f;
            float leaveY = buttonsY + ButtonH + 22f;
            float height = leaveY + ButtonH + Pad;

            var canvas = UIKit.CreateWorldCanvas("Settings Canvas", new Vector2(W, height), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 14f, modal: true);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            var title = UIKit.Label(p, "Title", "Settings", 14f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, W - 2 * Pad, HeaderH);

            m_Rows[0] = AddRow(p, "Music", rowsY, () => AudioLevels.Music, v => AudioLevels.Music = v);
            m_Rows[1] = AddRow(p, "Sound effects", rowsY + rowH + RowGap, () => AudioLevels.Effects, v => AudioLevels.Effects = v);
            m_Rows[2] = AddRow(p, "Tutorial voice", rowsY + 2 * (rowH + RowGap), () => AudioLevels.Voice, v => AudioLevels.Voice = v);
            m_Rows[3] = AddRow(p, "Master volume", rowsY + 3 * (rowH + RowGap), () => AudioLevels.Master, v => AudioLevels.Master = v);

            var language = UIKit.Label(p, "Language Label", "Language", 10f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(language.rectTransform, Pad, languageY, W - 2 * Pad, LabelH);
            float half = (W - 2 * Pad - 8f) / 2f;
            m_English = UIKit.Button(p, "English", string.Empty, 10f, false, () => SetLanguage(Language.English));
            UIKit.Place((RectTransform)m_English.transform, Pad, languageY + LabelH + 4f, half, ButtonH);
            m_Chinese = UIKit.Button(p, "Chinese", string.Empty, 10f, false, () => SetLanguage(Language.Chinese));
            UIKit.Place((RectTransform)m_Chinese.transform, Pad + half + 8f, languageY + LabelH + 4f, half, ButtonH);
            // Each language is always written in itself, so it can be found whatever is showing.
            m_English.GetComponentInChildren<TextMeshProUGUI>().SetPlain("English");
            m_Chinese.GetComponentInChildren<TextMeshProUGUI>().SetPlain("中文");

            // How to point: look and pinch (headsets with eye tracking), or each hand's ray.
            var input = UIKit.Label(p, "Pointing Label", "Pointing", 10f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(input.rectTransform, Pad, inputY, W - 2 * Pad, LabelH);
            m_Gaze = UIKit.Button(p, "Eyes", "Look and pinch", 10f, false, () => SetGaze(true), Icons.Eye);
            UIKit.Place((RectTransform)m_Gaze.transform, Pad, inputY + LabelH + 4f, half, ButtonH);
            m_Rays = UIKit.Button(p, "Rays", "Hand rays", 10f, false, () => SetGaze(false), Icons.Hand);
            UIKit.Place((RectTransform)m_Rays.transform, Pad + half + 8f, inputY + LabelH + 4f, half, ButtonH);

            var reset = UIKit.Button(p, "Reset", "Reset sound", 10f, false, AudioLevels.ResetToDefaults, Icons.Reset);
            UIKit.Place((RectTransform)reset.transform, Pad, buttonsY, half, ButtonH);
            var close = UIKit.Button(p, "Close", "Done", 10f, true, Close, Icons.Check);
            UIKit.Place((RectTransform)close.transform, Pad + half + 8f, buttonsY, half, ButtonH);

            var rule = UIKit.Panel(p, "Divider", new Color(0f, 0f, 0f, 0.1f), 0.5f);
            rule.raycastTarget = false;
            UIKit.Place(rule.rectTransform, Pad, leaveY - 12f, W - 2 * Pad, 1f);
            m_MainMenu = UIKit.Button(p, "Main Menu", "Main menu", 10f, false, () =>
            {
                Close();
                MainMenuRequested?.Invoke();
            }, Icons.Home);
            UIKit.Place((RectTransform)m_MainMenu.transform, Pad, leaveY, half, ButtonH);
            var quit = UIKit.Button(p, "Quit", "Quit app", 10f, false, () => QuitRequested?.Invoke(), Icons.Exit);
            UIKit.Place((RectTransform)quit.transform, Pad + half + 8f, leaveY, half, ButtonH);
            var quitLabel = quit.GetComponentInChildren<TextMeshProUGUI>();
            quitLabel.color = new Color(0.72f, 0.2f, 0.16f);
            quit.transform.Find("Icon").GetComponent<TextMeshProUGUI>().color = quitLabel.color;

            AudioLevels.Changed += Sync;
            Loc.Changed += Sync;
            Sync();
        }

        void SetGaze(bool on)
        {
            if (GazePinch.Instance != null)
                GazePinch.Instance.Enabled = on;
            else
                SaveData.GazeInput = on;
            Sync();
        }

        void SetLanguage(Language language)
        {
            Loc.Current = language;
            Sync();
        }

        /// <summary>Main menu is offered only away from it (not when Settings was opened from the welcome panel).</summary>
        public void ShowMainMenu(bool show) => m_MainMenu.interactable = show;

        Row AddRow(Transform p, string name, float y, Func<float> get, Action<float> set)
        {
            var row = new Row { get = get, set = set };
            var label = UIKit.Label(p, $"{name} Label", name, 10f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(label.rectTransform, Pad, y, W - 2 * Pad - 60f, LabelH);
            row.value = UIKit.Label(p, $"{name} Value", string.Empty, 10f, UIKit.MutedTextColor, TextAlignmentOptions.TopRight);
            UIKit.Place(row.value.rectTransform, W - Pad - 60f, y, 60f, LabelH);

            float sliderY = y + LabelH + 4f;
            var down = UIKit.Button(p, $"{name} Down", "-", 14f, false, () => row.set(Mathf.Round((row.get() - Step) / Step) * Step));
            UIKit.Place((RectTransform)down.transform, Pad, sliderY, StepW, SliderH);
            var up = UIKit.Button(p, $"{name} Up", "+", 14f, false, () => row.set(Mathf.Round((row.get() + Step) / Step) * Step));
            UIKit.Place((RectTransform)up.transform, W - Pad - StepW, sliderY, StepW, SliderH);

            float sliderX = Pad + StepW + 8f;
            row.slider = MakeSlider(p, name, sliderX, sliderY, W - 2 * sliderX, SliderH);
            row.slider.onValueChanged.AddListener(v =>
            {
                if (!m_Syncing)
                    row.set(v);
            });
            return row;
        }

        /// <summary>A slider with a hit area the full height of the row, a thin track and a round handle.</summary>
        static Slider MakeSlider(Transform parent, string name, float x, float y, float width, float height)
        {
            var root = new GameObject($"{name} Slider", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            UIKit.Place(root, x, y, width, height);

            // Transparent, so pointing anywhere along the row grabs the slider, not just the thin track.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            float handle = height * 0.8f, track = 6f;
            var trackImage = UIKit.Panel(root, "Track", new Color(0f, 0f, 0f, 0.14f), track / 2f);
            trackImage.raycastTarget = false;
            Inset(trackImage.rectTransform, handle / 2f, (height - track) / 2f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(root, false);
            Inset(fillArea, handle / 2f, (height - track) / 2f);
            var fill = UIKit.Panel(fillArea, "Fill", UIKit.PrimaryColor, track / 2f);
            fill.raycastTarget = false;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(root, false);
            Inset(handleArea, handle / 2f, (height - handle) / 2f);
            var knob = UIKit.Panel(handleArea, "Handle", Color.white, handle / 2f);
            knob.rectTransform.sizeDelta = new Vector2(handle, 0f);
            var ring = knob.gameObject.AddComponent<Outline>();
            ring.effectColor = UIKit.PrimaryColor;
            ring.effectDistance = new Vector2(1.2f, -1.2f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.colors = UIKit.SecondaryColors();
            return slider;
        }

        static void Inset(RectTransform rt, float horizontal, float vertical)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(horizontal, vertical);
            rt.offsetMax = new Vector2(-horizontal, -vertical);
        }

        void Sync()
        {
            m_Syncing = true;
            foreach (var row in m_Rows)
            {
                if (row == null)
                    continue;
                float v = row.get();
                row.slider.SetValueWithoutNotify(v);
                if (v <= 0.001f)
                    row.value.Localize("Off");
                else
                    row.value.SetPlain($"{Mathf.RoundToInt(v * 100f)}%");
            }
            if (m_English != null)
            {
                UIKit.SetChipSelected(m_English, Loc.IsEnglish);
                UIKit.SetChipSelected(m_Chinese, !Loc.IsEnglish);
            }
            if (m_Gaze != null)
            {
                // Without eye tracking (PICO 4 Ultra) only the rays work.
                bool supported = GazePinch.Instance != null && GazePinch.Instance.Supported;
                m_Gaze.interactable = supported;
                bool gaze = supported && SaveData.GazeInput;
                UIKit.SetChipSelected(m_Gaze, gaze);
                UIKit.SetChipSelected(m_Rays, !gaze);
            }
            m_Syncing = false;
        }

        public void Open()
        {
            Sync();
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, m_Head, distance, belowEyes);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void OnDisable() => AudioLevels.Save();

        void OnDestroy()
        {
            AudioLevels.Changed -= Sync;
            Loc.Changed -= Sync;
        }
    }
}
