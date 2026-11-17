using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// First thing the user sees: two ways in and nothing else to work out. "Start in my room" and "Try a
    /// sample room", or, when a room from last time is saved, "Continue last room" and "Start a new room".
    /// A scan prompt appears only when the headset has no room set-up, and a room-scale prompt only when its
    /// boundary is stationary (standing in place). Every way in first asks the user,
    /// each on a screen of its own, to tap their floor and then to stand up and look around the room, so
    /// the live 3D scan has seen the real furniture before anything is placed (both once a session, both
    /// skipped on an <see cref="Emulator"/>). The language switch and Settings sit by the title (leaving the app is in Settings). On a first visit the panel only explains the app and offers the guided tour. Spawns in front of the user at a
    /// comfortable height.
    /// </summary>
    public class WelcomePanel : MonoBehaviour
    {
        const float Width = 520f, Pad = 32f;
        const float HomeHeight = 334f;
        const float AboutH = 96f;

        [SerializeField] float distance = 1.1f;
        [SerializeField] float belowEyes = 0.15f;

        static readonly Color Attention = new Color(0.95f, 0.65f, 0.2f);
        static readonly Color Done = new Color(0.36f, 0.7f, 0.45f);
        static readonly Color NotYet = new Color(0f, 0f, 0f, 0.12f);

        // Look-around scan: the room is split into directions around the user, each needing a moment's
        // look while standing and looking a little down, where the furniture is.
        const int ScanDirections = 8;
        const float ScanDwellSeconds = 0.6f;
        const float StandingEyeHeight = 1.3f; // seated eyes are about 1.1–1.25 m above the floor
        const float MinLookDown = 8f, MaxLookDown = 70f;
        readonly float[] m_Looked = new float[ScanDirections];
        GameObject m_ScanScreen;
        bool m_Scanning;
        bool m_LookedAround; // once a session

        Canvas m_Canvas;
        Button m_PrimaryButton;
        Button m_SecondaryButton;
        Button m_LanguageButton;
        TextMeshProUGUI m_LanguageLabel;
        RectTransform m_About;
        TextMeshProUGUI m_PrimaryLabel;
        TextMeshProUGUI m_SecondaryLabel;
        Action m_AfterSetup; // the start option waiting on the floor tap and the look-around
        TextMeshProUGUI m_ScanNotice;
        Button m_ScanButton;
        TextMeshProUGUI m_BoundaryNotice;
        Button m_BoundaryButton;
        RoomScale.Boundary m_Boundary = RoomScale.Boundary.Unknown;
        GameObject m_HomeScreen;
        GameObject m_FloorScreen;
        TextMeshProUGUI m_FloorNext;
        FloorDetector m_Floor;
        FloorTapCalibrator m_Calibrator;
        SceneObstacles m_Scene;
        RoomScanner m_Scanner;
        Transform m_Head;
        bool m_SuggestScan;

        public event Action StartSampleRoomRequested;
        /// <summary>Straight into the user's own room: no sample furniture, just the dock.</summary>
        public event Action StartMyRoomRequested;
        /// <summary>Lay out the room the user was working on last time.</summary>
        public event Action ContinueRequested;
        /// <summary>First visit: set up, then the guided tour in the empty room.</summary>
        public event Action StartTutorialRequested;
        public event Action SettingsRequested;
        /// <summary>The user says they've set up a room-scale boundary; read it again.</summary>
        public event Action BoundaryCheckRequested;

        public void Build(Camera eventCamera, FloorDetector floor, FloorTapCalibrator calibrator, SceneObstacles scene, RoomScanner scanner)
        {
            m_Head = eventCamera.transform;
            m_Floor = floor;
            m_Calibrator = calibrator;
            m_Scene = scene;
            m_Scanner = scanner;

            m_Canvas = UIKit.CreateWorldCanvas("Welcome Canvas", new Vector2(Width, HomeHeight), transform, eventCamera);
            var root = m_Canvas.transform;

            var panel = UIKit.Window(root, 28f);
            UIKit.Stretch(panel.rectTransform);

            var home = NewScreen(panel.transform, "Home Screen");
            m_HomeScreen = home.gameObject;

            var title = UIKit.Label(home, "Title", "Palette Room", 46f, UIKit.TextColor, style: FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 34f;
            title.fontSizeMax = 46f;
            // Language and Settings by the title, kept compact so the title always fits on one line;
            // leaving the app is in Settings.
            const float HeaderButtonH = 44f, LanguageW = 108f, Gap = 10f;
            UIKit.Place(title.rectTransform, Pad, Pad, Width - 2 * Pad - LanguageW - HeaderButtonH - 2 * Gap, 56f);
            m_LanguageButton = UIKit.Button(home, "Language", string.Empty, 16f, false, ToggleLanguage, Icons.Language);
            UIKit.Place((RectTransform)m_LanguageButton.transform, Width - Pad - HeaderButtonH - Gap - LanguageW, Pad + 6f, LanguageW, HeaderButtonH);
            m_LanguageLabel = m_LanguageButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var settings = UIKit.IconTile(home, "Settings", Icons.Settings, null, HeaderButtonH, () => SettingsRequested?.Invoke());
            UIKit.Place((RectTransform)settings.transform, Width - Pad - HeaderButtonH, Pad + 6f, HeaderButtonH, HeaderButtonH);

            var body = UIKit.Label(home, "Body", "Restyle your real room with color and true-scale furniture.", 21f, UIKit.MutedTextColor);
            UIKit.Place(body.rectTransform, Pad, Pad + 66f, Width - 2 * Pad, 60f);

            // First visit: what the app does, in three lines, and one way in (the guided tour).
            m_About = new GameObject("About", typeof(RectTransform)).GetComponent<RectTransform>();
            m_About.SetParent(home, false);
            UIKit.Place(m_About, Pad, Pad + 132f, Width - 2 * Pad, AboutH);
            string[] about =
            {
                "Furnish your room with true-size pieces",
                "Sweep color palettes across the whole room",
                "Save your designs and send the shopping list",
            };
            for (int i = 0; i < about.Length; i++)
            {
                var dot = UIKit.Panel(m_About, $"Dot {i + 1}", UIKit.ThemeColor(i == 2 ? 4 : i * 2), 6f);
                dot.raycastTarget = false;
                UIKit.Place(dot.rectTransform, 2f, i * 32f + 8f, 12f, 12f);
                var line = UIKit.Label(m_About, $"Point {i + 1}", about[i], 18f, UIKit.TextColor);
                line.textWrappingMode = TextWrappingModes.NoWrap;
                line.enableAutoSizing = true;
                line.fontSizeMin = 14f;
                line.fontSizeMax = 18f;
                UIKit.Place(line.rectTransform, 26f, i * 32f, Width - 2 * Pad - 26f, 28f);
            }

            m_PrimaryButton = UIKit.Button(home, "Primary", string.Empty, 24f, true, OnPrimaryPressed);
            m_PrimaryLabel = m_PrimaryButton.GetComponentInChildren<TextMeshProUGUI>();

            m_SecondaryButton = UIKit.Button(home, "Secondary", string.Empty, 21f, false, OnSecondaryPressed);
            m_SecondaryLabel = m_SecondaryButton.GetComponentInChildren<TextMeshProUGUI>();

            // Optional notices below the start buttons, laid out in Refresh: room-scale boundary, then room scan.
            m_BoundaryNotice = UIKit.Label(home, "Boundary Notice",
                "Palette Room is made for walking around your room, but your headset has a stationary boundary. " +
                "Set up a custom (room-scale) boundary in the PICO boundary settings, then come back.",
                19f, UIKit.TextColor);
            m_BoundaryNotice.enableAutoSizing = true;
            m_BoundaryNotice.fontSizeMin = 15f;
            m_BoundaryNotice.fontSizeMax = 19f;
            m_BoundaryButton = UIKit.Button(home, "Check Boundary", "I've set it up, check again", 19f, false,
                () => BoundaryCheckRequested?.Invoke());
            m_ScanNotice = UIKit.Label(home, "Scan Notice", string.Empty, 19f, UIKit.TextColor);
            m_ScanButton = UIKit.Button(home, "Scan Room", "Scan my room", 19f, false, OnScanPressed);

            BuildFloorScreen(panel.transform);
            BuildScanScreen(panel.transform);

            m_Floor.FloorChanged += _ => Refresh();
            m_Calibrator.Completed += OnFloorSet;
            if (m_Scene != null)
                m_Scene.Changed += Refresh;
            if (m_Scanner != null)
                m_Scanner.Finished += _ => Refresh();
            Refresh();
        }

        static RectTransform NewScreen(Transform parent, string name)
        {
            var screen = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            screen.SetParent(parent, false);
            UIKit.Stretch(screen);
            return screen;
        }

        // ---- set-up screens: one short title, a moving picture, a progress bar -------------------
        // Modelled on PICO's own set-up flows: what to do is shown, not described.
        const float SetupHeight = 494f, Art = 250f, BarH = 16f;
        static readonly Color Track = new Color(0f, 0f, 0f, 0.08f);

        RectTransform m_Radar;           // world directions, turned so where the user faces is up
        Image[] m_Sectors;               // one per scan direction, filling as it's seen
        Image m_Cone;                    // the user's view, always straight up
        Image m_ScanBar;                 // overall progress
        TextMeshProUGUI m_ScanHint;      // only when something's wrong (sitting down)
        RectTransform m_FloorHand;       // bobs down to the floor line
        Image m_FloorRipple;

        /// <summary>Takes over the panel while the floor is being set, so the next step can't be missed.</summary>
        void BuildFloorScreen(Transform parent)
        {
            var screen = NewScreen(parent, "Floor Screen");
            m_FloorScreen = screen.gameObject;

            // A hand reaching down to a floor line, with a ripple where it touches.
            var art = new GameObject("Picture", typeof(RectTransform)).GetComponent<RectTransform>();
            art.SetParent(screen, false);
            UIKit.Place(art, Pad, Pad, Width - 2 * Pad, Art);
            var floorLine = UIKit.Panel(art, "Floor Line", new Color(0f, 0f, 0f, 0.25f), 2f);
            floorLine.raycastTarget = false;
            UIKit.Place(floorLine.rectTransform, 70f, Art - 46f, Width - 2 * Pad - 140f, 4f);
            m_FloorRipple = Ring(art, "Ripple", UIKit.ThemeColor(3));
            m_FloorRipple.rectTransform.anchoredPosition = new Vector2((Width - 2 * Pad) / 2f, -(Art - 44f));
            var hand = UIKit.Label(art, "Hand", Icons.Touch, 120f, UIKit.TextColor, TextAlignmentOptions.Center);
            hand.textWrappingMode = TextWrappingModes.NoWrap;
            m_FloorHand = hand.rectTransform;
            m_FloorHand.anchorMin = m_FloorHand.anchorMax = new Vector2(0.5f, 1f);
            m_FloorHand.pivot = new Vector2(0.5f, 0f);
            m_FloorHand.sizeDelta = new Vector2(140f, 140f);

            var title = UIKit.Label(screen, "Title", "Touch the floor", 34f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(title.rectTransform, Pad, Pad + Art + 6f, Width - 2 * Pad, 52f);
            var how = UIKit.Label(screen, "How", "Reach down and pinch. Or point at the floor and pinch.", 19f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(how.rectTransform, Pad, Pad + Art + 60f, Width - 2 * Pad, 30f);
            m_FloorNext = UIKit.Label(screen, "Step", string.Empty, 16f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(m_FloorNext.rectTransform, Pad, Pad + Art + 94f, Width - 2 * Pad, 24f);

            AddCancel(screen);
        }

        /// <summary>Stand up and look around: the headset's live 3D scan only knows what it has seen.</summary>
        void BuildScanScreen(Transform parent)
        {
            var screen = NewScreen(parent, "Scan Screen");
            m_ScanScreen = screen.gameObject;

            // A top-down view of the user in the middle of their room. The ring of directions turns as
            // they turn (where they face is always up), and each part fills in as they look at it.
            var art = new GameObject("Picture", typeof(RectTransform)).GetComponent<RectTransform>();
            art.SetParent(screen, false);
            UIKit.Place(art, Pad, Pad, Width - 2 * Pad, Art);
            var centre = new Vector2((Width - 2 * Pad) / 2f, -Art / 2f);

            m_Cone = new GameObject("View", typeof(RectTransform)).AddComponent<Image>();
            m_Cone.transform.SetParent(art, false);
            m_Cone.sprite = ConeSprite;
            m_Cone.color = new Color(UIKit.ThemeColor(1).r, UIKit.ThemeColor(1).g, UIKit.ThemeColor(1).b, 0.45f);
            m_Cone.raycastTarget = false;
            var cr = m_Cone.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(150f, 110f);
            cr.anchoredPosition = centre;

            m_Radar = new GameObject("Directions", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Radar.SetParent(art, false);
            m_Radar.anchorMin = m_Radar.anchorMax = new Vector2(0f, 1f);
            m_Radar.sizeDelta = new Vector2(Art - 10f, Art - 10f);
            m_Radar.anchoredPosition = centre;
            m_Sectors = new Image[ScanDirections];
            for (int i = 0; i < ScanDirections; i++)
            {
                var sector = new GameObject($"Direction {i + 1}", typeof(RectTransform)).AddComponent<Image>();
                sector.transform.SetParent(m_Radar, false);
                sector.sprite = SectorSprite;
                sector.raycastTarget = false;
                UIKit.Stretch(sector.rectTransform);
                // Sector i covers world headings i·45° to (i+1)·45°, clockwise from the start.
                sector.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(i + 0.5f) * 360f / ScanDirections);
                m_Sectors[i] = sector;
            }

            // The user, seen from above: head and the direction they face.
            var head = UIKit.Panel(art, "You", UIKit.PrimaryColor, 22f);
            head.raycastTarget = false;
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0f, 1f);
            head.rectTransform.sizeDelta = new Vector2(44f, 44f);
            head.rectTransform.anchoredPosition = centre;
            var nose = new GameObject("Facing", typeof(RectTransform)).AddComponent<Image>();
            nose.transform.SetParent(head.transform, false);
            nose.sprite = UIKit.ArrowSprite;
            nose.color = Color.white;
            nose.raycastTarget = false;
            nose.rectTransform.sizeDelta = new Vector2(20f, 20f);
            nose.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f); // the arrow sprite points down

            var title = UIKit.Label(screen, "Title", "Look around your room", 34f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(title.rectTransform, Pad, Pad + Art + 6f, Width - 2 * Pad, 52f);

            var track = UIKit.Panel(screen, "Progress Track", Track, BarH / 2f);
            track.raycastTarget = false;
            UIKit.Place(track.rectTransform, Pad + 40f, Pad + Art + 68f, Width - 2 * Pad - 80f, BarH);
            m_ScanBar = UIKit.Panel(track.transform, "Progress", Done, BarH / 2f);
            m_ScanBar.raycastTarget = false;
            var br = m_ScanBar.rectTransform;
            br.anchorMin = Vector2.zero;
            br.anchorMax = new Vector2(0f, 1f);
            br.offsetMin = br.offsetMax = Vector2.zero;

            m_ScanHint = UIKit.Label(screen, "Hint", "Please stand up", 18f, Attention, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(m_ScanHint.rectTransform, Pad, Pad + Art + 92f, Width - 2 * Pad, 26f);

            AddCancel(screen);
        }

        void AddCancel(Transform screen)
        {
            var cancel = UIKit.Button(screen, "Cancel", "Cancel", 18f, false, CancelSetup, Icons.Close);
            UIKit.Place((RectTransform)cancel.transform, (Width - 170f) / 2f, SetupHeight - Pad - 46f, 170f, 46f);
        }

        /// <summary>Animates the floor picture: the hand reaches down, the floor ripples where it lands.</summary>
        void AnimateFloorPicture()
        {
            if (m_FloorHand == null)
                return;
            float t = Mathf.Repeat(Time.unscaledTime / 1.8f, 1f);
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t * 2f, 1f));
            m_FloorHand.anchoredPosition = new Vector2(0f, -(Art - 50f) + 70f * (1f - reach) - 4f);
            // The ripple spreads out just after each touch.
            float since = Mathf.Repeat(t - 0.5f, 1f);
            float grow = Mathf.Clamp01(since * 2.2f);
            m_FloorRipple.rectTransform.sizeDelta = new Vector2(30f + 140f * grow, (30f + 140f * grow) * 0.32f);
            var c = m_FloorRipple.color;
            c.a = 0.8f * (1f - grow);
            m_FloorRipple.color = c;
        }

        static Image Ring(Transform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = RingSprite;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 1f);
            return image;
        }

        // ---- drawn shapes -----------------------------------------------------------------------

        static Sprite s_Sector, s_Cone, s_Ring;

        /// <summary>One 45° slice of a ring, pointing up, with a small gap to its neighbours.</summary>
        static Sprite SectorSprite => s_Sector != null ? s_Sector : (s_Sector = Field("Scan Sector", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Abs(Mathf.Atan2(x, y) * Mathf.Rad2Deg);
            float half = 180f / ScanDirections - 2.5f;
            float inRing = Mathf.Min(r - 0.62f, 0.96f - r);
            float inWedge = (half - angle) * Mathf.Deg2Rad * r;
            return Mathf.Min(inRing, inWedge);
        }));

        /// <summary>A soft field of view fanning up from the bottom centre.</summary>
        static Sprite ConeSprite => s_Cone != null ? s_Cone : (s_Cone = Field("Scan View", (x, y) =>
        {
            // x in −1..1, y in −1..1 across the sprite; the apex is at the bottom middle.
            float py = (y + 1f) / 2f;
            float angle = Mathf.Abs(Mathf.Atan2(x, py * 2f) * Mathf.Rad2Deg);
            float fade = 1f - py;
            return Mathf.Min((32f - angle) * 0.02f, fade * 0.5f);
        }, soft: true));

        static Sprite RingSprite => s_Ring != null ? s_Ring : (s_Ring = Field("Ripple", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Min(r - 0.8f, 0.96f - r);
        }));

        /// <summary>
        /// A white mipmapped sprite from a signed distance (positive inside) over −1..1 square coordinates;
        /// <paramref name="soft"/> uses the value itself as alpha.
        /// </summary>
        static Sprite Field(string name, System.Func<float, float, float> inside, bool soft = false)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            var px = new Color32[size * size];
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = inside(u, v);
                float a = soft ? Mathf.Clamp01(d * 2f) : Mathf.Clamp01(d / pixel + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            PlaceInFront();
            Refresh();
        }

        public void Hide()
        {
            m_AfterSetup = null;
            m_Scanning = false;
            if (m_Calibrator != null && m_Calibrator.IsActive)
                m_Calibrator.Cancel();
            gameObject.SetActive(false);
        }

        /// <summary>No room set-up was found: ask the user to scan their room before starting.</summary>
        public void SuggestScan()
        {
            m_SuggestScan = true;
            Refresh();
        }

        void PlaceInFront()
        {
            var forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();
            transform.position = m_Head.position + forward * distance + Vector3.down * belowEyes;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// Furniture stands on the floor, so a room is only entered once the user has tapped their real floor
        /// (a detected floor isn't trusted on its own). An emulator has no real floor to tap and skips this.
        /// </summary>
        bool FloorReady => m_Floor.State == FloorState.SetByTap || m_Floor.State == FloorState.Simulated || Emulator.IsActive;

        /// <summary>Nothing done yet: the panel just explains the app and offers the tour.</summary>
        static bool FirstVisit => !SaveData.TutorialSeen && !SaveData.HasLastRoom;

        void ToggleLanguage()
        {
            Loc.Current = Loc.IsEnglish ? Language.Chinese : Language.English;
            Refresh();
        }

        void OnPrimaryPressed()
        {
            // On a first visit the tour starts by itself once the floor and room scan are done.
            if (FirstVisit)
                Enter(() => StartMyRoomRequested?.Invoke());
            else if (SaveData.HasLastRoom)
                Enter(() => ContinueRequested?.Invoke());
            else
                Enter(() => StartMyRoomRequested?.Invoke());
        }

        void OnSecondaryPressed()
        {
            if (FirstVisit)
                Enter(() => StartSampleRoomRequested?.Invoke());
            else if (SaveData.HasLastRoom)
                Enter(() => StartMyRoomRequested?.Invoke());
            else
                Enter(() => StartSampleRoomRequested?.Invoke());
        }

        bool LookedAround => m_LookedAround || m_Floor.State == FloorState.Simulated || Emulator.IsActive;

        void Enter(Action start)
        {
            Debug.Log($"[PaletteRoom] Start pressed, floor state {m_Floor.State}, floor ready: {FloorReady}, looked around: {LookedAround}.");
            m_AfterSetup = start;
            Advance();
        }

        /// <summary>The next set-up step still to do, or into the room once both are done.</summary>
        void Advance()
        {
            if (m_AfterSetup == null)
                return;
            if (!FloorReady)
            {
                if (!m_Calibrator.IsActive)
                    m_Calibrator.Begin();
            }
            else if (!LookedAround)
            {
                if (!m_Scanning)
                {
                    m_Scanning = true;
                    Array.Clear(m_Looked, 0, m_Looked.Length);
                }
            }
            else
            {
                var start = m_AfterSetup;
                m_AfterSetup = null;
                Refresh();
                if (gameObject.activeSelf)
                    start();
                return;
            }
            Refresh();
        }

        void OnFloorSet()
        {
            Refresh();
            Advance();
        }

        void Update()
        {
            if (m_FloorScreen != null && m_FloorScreen.activeInHierarchy)
                AnimateFloorPicture();
            if (!m_Scanning)
                return;
            FollowHead();
            float eyeHeight = m_Head.position.y - m_Floor.FloorY;
            var look = m_Head.forward;
            float down = -Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg;
            bool standing = eyeHeight >= StandingEyeHeight;
            if (standing && down >= MinLookDown && down <= MaxLookDown)
            {
                float yaw = Mathf.Repeat(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 360f);
                int direction = Mathf.FloorToInt(yaw / (360f / ScanDirections)) % ScanDirections;
                m_Looked[direction] += Time.unscaledDeltaTime;
            }

            int seen = 0;
            float progress = 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < ScanDirections; i++)
            {
                float part = Mathf.Clamp01(m_Looked[i] / ScanDwellSeconds);
                progress += part / ScanDirections;
                bool done = part >= 1f;
                // Unseen directions breathe gently, inviting a look; seen ones settle in green.
                m_Sectors[i].color = done ? Done : Color.Lerp(NotYet, Done, part * 0.6f + (1f - part) * pulse * 0.12f);
                if (done)
                    seen++;
            }
            float facing = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            m_Radar.localRotation = Quaternion.Euler(0f, 0f, facing);
            m_ScanBar.rectTransform.anchorMax = new Vector2(Mathf.Lerp(m_ScanBar.rectTransform.anchorMax.x, progress, 0.25f), 1f);
            m_ScanHint.gameObject.SetActive(!standing);
            if (seen < ScanDirections)
                return;
            m_Scanning = false;
            m_LookedAround = true;
            Debug.Log("[PaletteRoom] Looked around the room.");
            PlaceInFront();
            Advance();
        }

        /// <summary>While the user turns around, the instructions come round with them.</summary>
        void FollowHead()
        {
            var forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                return;
            forward.Normalize();
            var target = m_Head.position + forward * distance + Vector3.down * belowEyes;
            float k = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward, Vector3.up), k);
        }

        void OnScanPressed()
        {
            m_Scanner.Launch();
            Refresh();
        }

        void CancelSetup()
        {
            m_AfterSetup = null;
            m_Scanning = false;
            if (m_Calibrator.IsActive)
                m_Calibrator.Cancel();
            PlaceInFront();
            Refresh();
        }

        void Refresh()
        {
            if (m_Floor == null || m_PrimaryLabel == null)
                return;

            bool settingFloor = m_Calibrator.IsActive;
            bool scanning = !settingFloor && m_Scanning;
            m_HomeScreen.SetActive(!settingFloor && !scanning);
            m_FloorScreen.SetActive(settingFloor);
            m_ScanScreen.SetActive(scanning);
            if (settingFloor || scanning)
            {
                SetHeight(SetupHeight);
                // Two steps before the room opens: the floor, then the look around.
                if (m_AfterSetup == null || LookedAround)
                    m_FloorNext.SetPlain(string.Empty);
                else
                    m_FloorNext.Localize("Step {0} of {1}", settingFloor ? 1 : 2, 2);
                return;
            }

            bool hasRoom = SaveData.HasLastRoom;
            bool first = FirstVisit;
            m_LanguageLabel.SetPlain(Loc.IsEnglish ? "中文" : "English");
            m_About.gameObject.SetActive(first);
            if (first)
            {
                m_PrimaryLabel.Localize("Get started");
                m_SecondaryLabel.Localize("Try a sample room");
            }
            else
            {
                m_PrimaryLabel.Localize(hasRoom ? "Continue last room" : "Start in my room");
                m_SecondaryLabel.Localize(hasRoom ? "Start a new room" : "Try a sample room");
            }
            float top = Pad + 132f + (first ? AboutH + 12f : 0f);
            UIKit.Place((RectTransform)m_PrimaryButton.transform, Pad, top, Width - 2 * Pad, 66f);
            UIKit.Place((RectTransform)m_SecondaryButton.transform, Pad, top + 78f, Width - 2 * Pad, first ? 48f : 56f);
            float homeHeight = top + 78f + (first ? 48f : 56f) + Pad;
            float noticesY = homeHeight - Pad + 18f;

            bool capturing = m_Scanner != null && m_Scanner.Busy;
            bool needsScan = RoomScanner.Available && m_SuggestScan && !HasRoomScan;
            m_PrimaryButton.interactable = !capturing;
            m_SecondaryButton.interactable = !capturing;
            m_ScanNotice.Localize(capturing ? "Scanning your room in PICO room set-up…" : "Scan your room so furniture fits around your walls and real furniture.");
            m_ScanButton.interactable = !capturing;

            // Stack whichever notices apply under the start buttons and size the panel to fit.
            float y = noticesY;
            bool boundary = m_Boundary == RoomScale.Boundary.Stationary;
            y = PlaceNotice(m_BoundaryNotice, m_BoundaryButton.gameObject, boundary, true, y, 100f);
            y = PlaceNotice(m_ScanNotice, m_ScanButton.gameObject, capturing || needsScan, needsScan, y, 50f);
            SetHeight(y > noticesY ? y - NoticeGap + Pad : homeHeight);
        }

        const float NoticeGap = 16f, NoticeButtonH = 52f;

        /// <summary>Shows or hides a notice and its button at <paramref name="y"/>; returns where the next one goes.</summary>
        float PlaceNotice(TextMeshProUGUI notice, GameObject button, bool show, bool showButton, float y, float noticeH)
        {
            notice.gameObject.SetActive(show);
            button.SetActive(show && showButton);
            if (!show)
                return y;
            UIKit.Place(notice.rectTransform, Pad, y, Width - 2 * Pad, noticeH);
            y += noticeH + 4f;
            if (showButton)
            {
                UIKit.Place((RectTransform)button.transform, Pad, y, Width - 2 * Pad, NoticeButtonH);
                y += NoticeButtonH;
            }
            return y + NoticeGap;
        }

        /// <summary>The headset's boundary, from <see cref="RoomScale.Check"/>. A stationary one gets a notice asking for room-scale.</summary>
        public void SetBoundary(RoomScale.Boundary boundary)
        {
            m_Boundary = boundary;
            Refresh();
        }

        bool HasRoomScan => m_Scene != null && m_Scene.WallCount > 0;

        void SetHeight(float height) => ((RectTransform)m_Canvas.transform).sizeDelta = new Vector2(Width, height);
    }
}
