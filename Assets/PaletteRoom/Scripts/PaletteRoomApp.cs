using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Entry point: welcome panel (with a room-scan prompt when the room isn't set up) → "Start in my room"
    /// or "Start with a sample room" → palette dock, with the color wheel and shopping list opened from the
    /// dock. Owns floor detection and keeps furniture resting on the floor.
    /// </summary>
    [RequireComponent(typeof(PaletteManager), typeof(FloorDetector), typeof(FloorTapCalibrator))]
    public class PaletteRoomApp : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [FormerlySerializedAs("sampleRoom")]
        [SerializeField] RoomFurniture room;
        [SerializeField] WelcomePanel welcomePanel;
        [SerializeField] PaletteDock paletteDock;
        [SerializeField] ColorWheelPanel colorWheel;
        [SerializeField] ShoppingListPanel shoppingList;
        [SerializeField] Transform floorReticle;

        [Header("Editor test mode (Play mode without a headset)")]
        [Tooltip("Skip floor detection, show menus straight away, allow mouse clicks, right-drag to look. Ignored on device and when a headset is streaming.")]
        [SerializeField] bool editorTestMode = true;
        [Tooltip("Editor test mode: spawn the sample room and open the dock on Play.")]
        [SerializeField] bool autoStartSampleRoom;

        PaletteManager m_Palettes;
        FloorDetector m_Floor;
        FloorTapCalibrator m_Calibrator;
        Camera m_Camera;
        MovablePanel m_DockPanel;
        ObjectColorPanel m_ObjectColor;
        RoomColorScanner m_ColorScanner;
        RoomMatchBanner m_MatchBanner;
        SceneObstacles m_Scene;
        RoomScanner m_Scanner;

        [Tooltip("Seconds after start-up with no scanned walls before the welcome panel asks the user to scan their room.")]
        [SerializeField] float roomScanPromptDelay = 6f;

        bool TestModeActive => Application.isEditor && editorTestMode && !XRSettings.isDeviceActive;

        void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();
            m_Camera = xrOrigin != null ? xrOrigin.Camera : Camera.main;
            UIKit.MouseInput = TestModeActive; // must be set before any canvas is built

            m_Palettes = GetComponent<PaletteManager>();
            m_Floor = GetComponent<FloorDetector>();
            m_Calibrator = GetComponent<FloorTapCalibrator>();
            m_Calibrator.Configure(m_Floor, m_Camera.transform, floorReticle);

            EnsureEventSystem();

            m_Scene = GetComponent<SceneObstacles>();
            m_Scanner = gameObject.AddComponent<RoomScanner>();
            welcomePanel.Build(m_Camera, m_Floor, m_Calibrator, m_Scene, m_Scanner);
            welcomePanel.StartSampleRoomRequested += StartSampleRoom;
            welcomePanel.StartMyRoomRequested += StartMyRoom;
            welcomePanel.gameObject.SetActive(false);

            paletteDock.Build(m_Camera, m_Palettes, m_Calibrator);
            // World-anchored and grabbable; the menu button (M in the Editor) recalls it front-left.
            m_DockPanel = AddMovable(paletteDock.gameObject, recallable: true);
            m_DockPanel.SetRecallPose(0.7f, -30f, 0.2f);
            paletteDock.ColorWheelRequested += OpenColorWheel;
            paletteDock.ShoppingListRequested += OpenShoppingList;
            paletteDock.gameObject.SetActive(false);

            colorWheel.Build(m_Camera, m_Palettes);
            AddMovable(colorWheel.gameObject, recallable: false);
            colorWheel.Closed += ShowDock;
            colorWheel.gameObject.SetActive(false);

            shoppingList.Build(m_Camera, m_Palettes, room);
            AddMovable(shoppingList.gameObject, recallable: false);
            shoppingList.AddRequested += (kind, variant) => room.Add(kind, variant, m_Camera.transform, m_Floor.FloorY);
            shoppingList.PickUpRequested += PickUpFromList;
            shoppingList.Closed += ShowDock;
            shoppingList.gameObject.SetActive(false);

            // Click (rather than drag) a piece to choose its colors.
            var colorGo = new GameObject("Object Color Panel");
            colorGo.transform.SetParent(transform, false);
            m_ObjectColor = colorGo.AddComponent<ObjectColorPanel>();
            m_ObjectColor.Build(m_Camera, m_Palettes);
            AddMovable(colorGo, recallable: false);
            colorGo.SetActive(false);
            FurnitureClickDetector.Clicked += OpenObjectColor;

            // On start-up, read the room's colors and switch to the palette that suits them best.
            var bannerGo = new GameObject("Room Match Banner");
            bannerGo.transform.SetParent(transform, false);
            m_MatchBanner = bannerGo.AddComponent<RoomMatchBanner>();
            m_MatchBanner.Build(m_Camera);
            m_ColorScanner = gameObject.AddComponent<RoomColorScanner>();
            m_ColorScanner.Matched += OnRoomColorsMatched;
            m_ColorScanner.Failed += _ => m_MatchBanner.ShowUnavailable(m_Palettes.Current);

            // Picking a palette sends the colors out across the room as a wave (with chimes).
            var wave = GetComponent<PaletteWave>();
            if (wave == null)
                wave = gameObject.AddComponent<PaletteWave>();
            wave.Configure(m_Camera.transform, room.BaseMaterial);

            BuildBriefsAndLookbook();
            m_Floor.FloorChanged += OnFloorChanged;
        }

        // ---- design briefs, reveal, lookbook ------------------------------------------------------

        BriefsPanel m_Briefs;
        BriefTracker m_Tracker;
        LookbookPanel m_Lookbook;
        SendListPanel m_SendList;
        RevealSequence m_Reveal;

        void BuildBriefsAndLookbook()
        {
            m_Briefs = NewPanel<BriefsPanel>("Design Briefs Panel");
            m_Briefs.Build(m_Camera);
            AddMovable(m_Briefs.gameObject, recallable: false);
            m_Briefs.gameObject.SetActive(false);
            m_Briefs.StartRequested += StartBrief;
            m_Briefs.Closed += ShowDock;

            m_Tracker = NewPanel<BriefTracker>("Brief Tracker");
            m_Tracker.Build(m_Camera, room, m_Palettes);
            var trackerMove = AddMovable(m_Tracker.gameObject, recallable: false);
            trackerMove.SetRecallPose(0.75f, 32f, 0.15f); // front-right, opposite the dock
            m_Tracker.RevealRequested += () => m_Reveal.Play(m_Tracker.Brief, m_Tracker.Stars);
            m_Tracker.ExitRequested += m_Tracker.End;

            m_Lookbook = NewPanel<LookbookPanel>("Lookbook Panel");
            m_Lookbook.Build(m_Camera);
            AddMovable(m_Lookbook.gameObject, recallable: false);
            m_Lookbook.gameObject.SetActive(false);
            m_Lookbook.Closed += ShowDock;
            m_Lookbook.RestoreRequested += RestoreFromLookbook;

            m_SendList = NewPanel<SendListPanel>("Send List Panel");
            m_SendList.Build(m_Camera);
            AddMovable(m_SendList.gameObject, recallable: false);
            m_SendList.gameObject.SetActive(false);
            // The send panel replaces the lookbook where it stands (two panels on top of each other
            // blend into an unreadable white blob), and closing it brings the lookbook back.
            m_Lookbook.SendRequested += (entry, card) =>
            {
                m_SendList.Open(entry, m_Lookbook.transform);
                m_Lookbook.gameObject.SetActive(false);
            };
            m_SendList.Closed += () => m_Lookbook.gameObject.SetActive(true);
            m_Lookbook.RevealCurrentRequested += () =>
            {
                m_Lookbook.gameObject.SetActive(false);
                ShowDock();
                m_Reveal.Play(m_Tracker.Brief, m_Tracker.Brief != null ? m_Tracker.Stars : 0);
            };

            m_Reveal = gameObject.AddComponent<RevealSequence>();
            m_Reveal.Configure(room, m_Palettes, m_Camera, RevealHides);

            paletteDock.BriefsRequested += () => { HideFloatingPanels(); m_Briefs.Open(m_Camera.transform); };
            paletteDock.LookbookRequested += () => { HideFloatingPanels(); m_Lookbook.Open(m_Camera.transform); };
        }

        T NewPanel<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>Everything the reveal clears out of the way (and puts back afterwards).</summary>
        IEnumerable<GameObject> RevealHides()
        {
            yield return paletteDock.gameObject;
            yield return colorWheel.gameObject;
            yield return shoppingList.gameObject;
            yield return m_ObjectColor.gameObject;
            yield return m_MatchBanner.gameObject;
            yield return m_Briefs.gameObject;
            yield return m_Lookbook.gameObject;
            yield return m_Tracker.gameObject;
            yield return m_SendList.gameObject;
        }

        void HideFloatingPanels()
        {
            paletteDock.gameObject.SetActive(false);
            colorWheel.gameObject.SetActive(false);
            shoppingList.gameObject.SetActive(false);
            m_Briefs.gameObject.SetActive(false);
            m_Lookbook.gameObject.SetActive(false);
        }

        /// <summary>
        /// A shopping-list picture was held: add the piece and put it straight into the hand that held
        /// it, so the user carries it into the room and lets go where they want it.
        /// </summary>
        void PickUpFromList(FurnitureKind kind, string variant, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor hand)
        {
            var item = room.Add(kind, variant, m_Camera.transform, m_Floor.FloorY, dropIn: hand == null);
            if (hand == null)
                return; // the mouse in Editor test mode: the piece is just added
            shoppingList.gameObject.SetActive(false); // out of the way, so the user sees where it's going
            ShowDock();
            StartCoroutine(GiveToHand(item, hand));
        }

        IEnumerator GiveToHand(FurnitureItem item, UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor hand)
        {
            // One frame so the new piece's own grab rules (floor, overlaps) are registered first.
            yield return null;
            if (item == null)
                yield break;
            var grab = item.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var manager = grab != null ? grab.interactionManager : null;
            if (manager == null)
                yield break;
            if (hand.hasSelection)
                manager.SelectExit(hand, hand.interactablesSelected[0]);
            manager.SelectEnter(hand, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            Debug.Log($"[PaletteRoom] Picked {item.DisplayName} up from the shopping list.");
        }

        void StartBrief(DesignBrief brief)
        {
            // A brief works on the room as it is: whatever is already placed counts toward the goals.
            welcomePanel.Hide();
            room.MarkEdited();
            m_Briefs.gameObject.SetActive(false);
            m_Tracker.Begin(brief);
            m_Tracker.GetComponent<MovablePanel>().PlaceInFront();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
            Debug.Log($"[PaletteRoom] Started brief \"{brief.title}\".");
        }

        void RestoreFromLookbook(Lookbook.Entry entry)
        {
            m_Lookbook.gameObject.SetActive(false);
            m_SendList.gameObject.SetActive(false);
            m_Tracker.End();
            welcomePanel.Hide();
            room.Restore(entry, m_Camera.transform, m_Floor.FloorY);
            var palette = Lookbook.PaletteOf(entry);
            if (palette != null)
                m_Palettes.SetPalette(palette);
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
        }

        void OnDestroy() => FurnitureClickDetector.Clicked -= OpenObjectColor;

        void OpenObjectColor(FurnitureItem item)
        {
            if (item == null || welcomePanel.gameObject.activeSelf)
                return;
            m_ObjectColor.Open(item);
        }

        IEnumerator Start()
        {
            if (TestModeActive)
            {
                m_Camera.gameObject.AddComponent<EditorTestCamera>();
                m_Floor.UseSimulatedFloor();
                yield return null; // let the camera stand up before placing panels
                welcomePanel.Show();
                if (autoStartSampleRoom)
                    StartSampleRoom();
                Debug.Log("[PaletteRoom] Editor test mode: floor simulated, mouse clicks on, right-drag to look.");
                yield break;
            }

            // Head pose reads as the origin for the first frames on device; wait for real tracking
            // so the welcome panel lands in front of the user at eye height.
            float timeout = Time.realtimeSinceStartup + 2f;
            var head = m_Camera.transform;
            while (head.localPosition.sqrMagnitude < 0.0001f && Time.realtimeSinceStartup < timeout)
                yield return null;
            yield return null;
            welcomePanel.Show();
            m_MatchBanner.ShowScanning();
            m_ColorScanner.Scan();

            // The saved room set-up arrives through plane detection within a few seconds. If no walls have
            // come by then, the room hasn't been set up: ask for a scan before the user starts.
            float promptAt = Time.realtimeSinceStartup + roomScanPromptDelay;
            while (Time.realtimeSinceStartup < promptAt && (m_Scene == null || m_Scene.WallCount == 0))
                yield return null;
            if (m_Scene != null && m_Scene.WallCount == 0 && welcomePanel.gameObject.activeSelf)
            {
                welcomePanel.SuggestScan();
                Debug.Log("[PaletteRoom] No room set-up found; suggesting a room scan.");
            }
        }

        // The room's colors change as the result appears, not before the spinner has had its moment.
        void OnRoomColorsMatched(RoomColorScanner.Match match) =>
            m_MatchBanner.Show(match, () => m_Palettes.SetPalette(match.palette));

        void StartSampleRoom()
        {
            room.SpawnSampleRoom(m_Camera.transform, m_Floor.FloorY);
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
        }

        /// <summary>The user's own room as it is: nothing is placed, the dock opens and pieces come from the shopping list.</summary>
        void StartMyRoom()
        {
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
            Debug.Log("[PaletteRoom] Started in the user's own room.");
        }

        MovablePanel AddMovable(GameObject panel, bool recallable)
        {
            var movable = panel.AddComponent<MovablePanel>();
            movable.Setup(m_Camera.transform, m_Camera, recallable);
            return movable;
        }

        // One floating panel at a time keeps the view of the room clear. The dock sits closer to the
        // user than the big panels, so it steps aside while one is open and returns when it closes.
        void OpenColorWheel()
        {
            paletteDock.gameObject.SetActive(false);
            shoppingList.gameObject.SetActive(false);
            colorWheel.Open();
        }

        void OpenShoppingList()
        {
            paletteDock.gameObject.SetActive(false);
            colorWheel.gameObject.SetActive(false);
            shoppingList.Open();
        }

        void ShowDock()
        {
            bool bigPanelOpen = colorWheel.gameObject.activeSelf || shoppingList.gameObject.activeSelf ||
                                (m_Briefs != null && m_Briefs.gameObject.activeSelf) || (m_Lookbook != null && m_Lookbook.gameObject.activeSelf);
            if (!bigPanelOpen)
                paletteDock.gameObject.SetActive(true);
        }

        void OnFloorChanged(FloorDetector floor)
        {
            room.SetFloor(floor.FloorY);
        }

        static void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            // XRI's input module drives world-space UI from hands and controllers alike.
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (!(module is XRUIInputModule))
                    Destroy(module);
            }
            if (eventSystem.GetComponent<XRUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }
    }
}
