using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// First thing the user sees: two ways in and nothing else to work out. "Start in my room" and "Try a
    /// sample room", or, when a room from last time is saved, "Continue last room" and "Start a new room".
    /// A scan prompt appears only when the headset has no room set-up. Every way in first asks the user,
    /// each on a screen of its own, to tap their floor and then to stand up and look around the room, so
    /// the live 3D scan has seen the real furniture before anything is placed (both once a session, both
    /// skipped on an <see cref="Emulator"/>). Exit sits by the title. Spawns in front of the user at a
    /// comfortable height.
    /// </summary>
    public class WelcomePanel : MonoBehaviour
    {
        const float Width = 520f, Pad = 32f;
        const float HomeHeight = 334f, HomeWithScanHeight = 454f, FloorHeight = 650f;
        const float ScanY = HomeHeight - 18f;

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
        readonly Image[] m_ScanDots = new Image[ScanDirections];
        GameObject m_ScanScreen;
        TextMeshProUGUI m_ScanHow;
        TextMeshProUGUI m_ScanProgress;
        bool m_Scanning;
        bool m_LookedAround; // once a session

        Canvas m_Canvas;
        Button m_PrimaryButton;
        Button m_SecondaryButton;
        TextMeshProUGUI m_PrimaryLabel;
        TextMeshProUGUI m_SecondaryLabel;
        Action m_AfterSetup; // the start option waiting on the floor tap and the look-around
        TextMeshProUGUI m_ScanNotice;
        Button m_ScanButton;
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
        public event Action ExitRequested;

        public void Build(Camera eventCamera, FloorDetector floor, FloorTapCalibrator calibrator, SceneObstacles scene, RoomScanner scanner)
        {
            m_Head = eventCamera.transform;
            m_Floor = floor;
            m_Calibrator = calibrator;
            m_Scene = scene;
            m_Scanner = scanner;

            m_Canvas = UIKit.CreateWorldCanvas("Welcome Canvas", new Vector2(Width, HomeHeight), transform, eventCamera);
            var root = m_Canvas.transform;

            var panel = UIKit.Panel(root, "Panel", UIKit.PanelColor, 28f);
            UIKit.Stretch(panel.rectTransform);

            var home = NewScreen(panel.transform, "Home Screen");
            m_HomeScreen = home.gameObject;

            var title = UIKit.Label(home, "Title", "Palette Room", 46f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, Width - 2 * Pad - 110f, 56f);
            var exit = UIKit.Button(home, "Exit", "Exit", 17f, false, () => ExitRequested?.Invoke());
            UIKit.Place((RectTransform)exit.transform, Width - Pad - 96f, Pad + 8f, 96f, 40f);

            var body = UIKit.Label(home, "Body", "Restyle your real room with color and true-scale furniture.", 21f, UIKit.MutedTextColor);
            UIKit.Place(body.rectTransform, Pad, Pad + 66f, Width - 2 * Pad, 60f);

            m_PrimaryButton = UIKit.Button(home, "Primary", string.Empty, 24f, true, OnPrimaryPressed);
            UIKit.Place((RectTransform)m_PrimaryButton.transform, Pad, Pad + 138f, Width - 2 * Pad, 66f);
            m_PrimaryLabel = m_PrimaryButton.GetComponentInChildren<TextMeshProUGUI>();

            m_SecondaryButton = UIKit.Button(home, "Secondary", string.Empty, 21f, false, OnSecondaryPressed);
            UIKit.Place((RectTransform)m_SecondaryButton.transform, Pad, Pad + 216f, Width - 2 * Pad, 56f);
            m_SecondaryLabel = m_SecondaryButton.GetComponentInChildren<TextMeshProUGUI>();

            m_ScanNotice = UIKit.Label(home, "Scan Notice", string.Empty, 19f, UIKit.TextColor);
            UIKit.Place(m_ScanNotice.rectTransform, Pad, ScanY, Width - 2 * Pad, 50f);
            m_ScanButton = UIKit.Button(home, "Scan Room", "Scan my room", 19f, false, OnScanPressed);
            UIKit.Place((RectTransform)m_ScanButton.transform, Pad, ScanY + 54f, Width - 2 * Pad, 52f);

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

        /// <summary>Takes over the panel while the floor is being set, so the next step can't be missed.</summary>
        void BuildFloorScreen(Transform parent)
        {
            var screen = NewScreen(parent, "Floor Screen");
            m_FloorScreen = screen.gameObject;

            var title = UIKit.Label(screen, "Title", "Set your floor height", 46f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, Width - 2 * Pad, 112f);
            var why = UIKit.Label(screen, "Why",
                "Furniture stands on your real floor, so Palette Room needs to know where it is.",
                22f, UIKit.MutedTextColor);
            UIKit.Place(why.rectTransform, Pad, Pad + 124f, Width - 2 * Pad, 70f);
            var how = UIKit.Label(screen, "How",
                "Reach down, touch the floor with your hand, and pinch your thumb and finger together.\n\nOr point at the floor and pinch. With a controller, pull the trigger.",
                25f, UIKit.TextColor);
            UIKit.Place(how.rectTransform, Pad, Pad + 214f, Width - 2 * Pad, 200f);
            m_FloorNext = UIKit.Label(screen, "Next", string.Empty, 21f, Attention);
            UIKit.Place(m_FloorNext.rectTransform, Pad, Pad + 428f, Width - 2 * Pad, 40f);

            var cancel = UIKit.Button(screen, "Cancel", "Cancel", 21f, false, CancelSetup);
            UIKit.Place((RectTransform)cancel.transform, Pad, FloorHeight - Pad - 56f, Width - 2 * Pad, 56f);
        }

        /// <summary>Stand up and look around: the headset's live 3D scan only knows what it has seen.</summary>
        void BuildScanScreen(Transform parent)
        {
            var screen = NewScreen(parent, "Scan Screen");
            m_ScanScreen = screen.gameObject;

            var title = UIKit.Label(screen, "Title", "Look around your room", 46f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, Width - 2 * Pad, 112f);
            var why = UIKit.Label(screen, "Why",
                "Furniture is placed around your real bed, sofa and tables, so the headset needs to see them first.",
                22f, UIKit.MutedTextColor);
            UIKit.Place(why.rectTransform, Pad, Pad + 124f, Width - 2 * Pad, 80f);
            m_ScanHow = UIKit.Label(screen, "How", string.Empty, 25f, UIKit.TextColor);
            UIKit.Place(m_ScanHow.rectTransform, Pad, Pad + 214f, Width - 2 * Pad, 150f);

            const float dot = 30f, gap = 14f;
            float rowX = (Width - (ScanDirections * dot + (ScanDirections - 1) * gap)) / 2f;
            for (int i = 0; i < ScanDirections; i++)
            {
                m_ScanDots[i] = UIKit.Panel(screen, $"Direction {i + 1}", NotYet, dot / 2f);
                m_ScanDots[i].raycastTarget = false;
                UIKit.Place(m_ScanDots[i].rectTransform, rowX + i * (dot + gap), Pad + 380f, dot, dot);
            }
            m_ScanProgress = UIKit.Label(screen, "Progress", string.Empty, 21f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Place(m_ScanProgress.rectTransform, Pad, Pad + 424f, Width - 2 * Pad, 40f);

            var cancel = UIKit.Button(screen, "Cancel", "Cancel", 21f, false, CancelSetup);
            UIKit.Place((RectTransform)cancel.transform, Pad, FloorHeight - Pad - 56f, Width - 2 * Pad, 56f);
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

        void OnPrimaryPressed()
        {
            if (SaveData.HasLastRoom)
                Enter(() => ContinueRequested?.Invoke());
            else
                Enter(() => StartMyRoomRequested?.Invoke());
        }

        void OnSecondaryPressed()
        {
            if (SaveData.HasLastRoom)
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
            for (int i = 0; i < ScanDirections; i++)
            {
                bool done = m_Looked[i] >= ScanDwellSeconds;
                m_ScanDots[i].color = done ? Done : NotYet;
                if (done)
                    seen++;
            }
            m_ScanHow.text = standing
                ? "Turn slowly all the way around, looking down a little at the floor and furniture."
                : "Please stand up, so the headset can see over your furniture.";
            m_ScanHow.color = standing ? UIKit.TextColor : Attention;
            m_ScanProgress.text = $"{seen} of {ScanDirections} directions seen";
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
                SetHeight(FloorHeight);
                m_FloorNext.text = m_AfterSetup == null ? string.Empty
                    : LookedAround ? "Your room opens as soon as this is done."
                    : "Then a quick look around the room, and you're in.";
                return;
            }

            bool hasRoom = SaveData.HasLastRoom;
            m_PrimaryLabel.text = hasRoom ? "Continue last room" : "Start in my room";
            m_SecondaryLabel.text = hasRoom ? "Start a new room" : "Try a sample room";

            bool capturing = m_Scanner != null && m_Scanner.Busy;
            bool needsScan = RoomScanner.Available && m_SuggestScan && !HasRoomScan;
            m_PrimaryButton.interactable = !capturing;
            m_SecondaryButton.interactable = !capturing;
            m_ScanNotice.gameObject.SetActive(capturing || needsScan);
            m_ScanNotice.text = capturing ? "Scanning your room in PICO room set-up…" : "Scan your room so furniture fits around your walls and real furniture.";
            m_ScanButton.gameObject.SetActive(needsScan);
            m_ScanButton.interactable = !capturing;
            SetHeight(capturing || needsScan ? HomeWithScanHeight : HomeHeight);
        }

        bool HasRoomScan => m_Scene != null && m_Scene.WallCount > 0;

        void SetHeight(float height) => ((RectTransform)m_Canvas.transform).sizeDelta = new Vector2(Width, height);
    }
}
