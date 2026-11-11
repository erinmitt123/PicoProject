using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Entry point: a short intro → welcome panel (on a first visit it only explains the app and offers the
    /// guided tour; a room-scan prompt when the room isn't set up) → the user's room, the sample room or last
    /// time's room → palette dock, with the color wheel and shopping list opened from the dock. Owns floor
    /// detection and keeps furniture resting on the floor. Also runs the guided tour (which furnishes an empty
    /// room piece by piece, pointing at each control), Settings (sound, language, main menu, quit), and
    /// saving the room in progress so it can be continued next time. Runs room-scale (floor tracking, Stage
    /// Mode, a prompt for a room-scale boundary when the headset's is stationary) and plays background music,
    /// which moves into a radio when one is placed.
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
            RoomScale.UseRoomScaleTracking(xrOrigin);
            AudioLevels.Ensure();
            CjkFont.Ensure(); // Chinese glyphs, before any label is made
            Icons.Ensure();   // button icons, likewise

            m_Palettes = GetComponent<PaletteManager>();
            // Every window carries a strip of the palette in use.
            UIKit.SetThemePalette(m_Palettes.Current);
            m_Palettes.PaletteChanged += UIKit.SetThemePalette;
            m_Floor = GetComponent<FloorDetector>();
            m_Calibrator = GetComponent<FloorTapCalibrator>();
            m_Calibrator.Configure(m_Floor, m_Camera.transform, floorReticle);

            EnsureEventSystem();
            TightenHandReach();
            // Look and pinch where the headset tracks the eyes; hand rays otherwise.
            gameObject.AddComponent<GazePinch>().Setup(m_Camera.transform);

            m_Scene = GetComponent<SceneObstacles>();
            m_Scanner = gameObject.AddComponent<RoomScanner>();
            welcomePanel.Build(m_Camera, m_Floor, m_Calibrator, m_Scene, m_Scanner);
            welcomePanel.StartSampleRoomRequested += StartSampleRoom;
            welcomePanel.StartMyRoomRequested += StartMyRoom;
            welcomePanel.ContinueRequested += ContinueLastRoom;
            welcomePanel.StartTutorialRequested += StartTutorial;
            welcomePanel.SettingsRequested += OpenSettings;
            welcomePanel.BoundaryCheckRequested += CheckBoundary;
            welcomePanel.gameObject.SetActive(false);

            paletteDock.Build(m_Camera, m_Palettes, m_Calibrator);
            // World-anchored and grabbable; the menu button (M in the Editor) recalls it front-left.
            m_DockPanel = AddMovable(paletteDock.gameObject, recallable: true);
            // The palette with its two side rails is about 70 cm across: a little further off, nearer the middle.
            m_DockPanel.SetRecallPose(0.85f, -20f, 0.2f);
            paletteDock.ColorWheelRequested += OpenColorWheel;
            paletteDock.ShoppingListRequested += OpenShoppingList;
            paletteDock.TutorialRequested += StartTutorial;
            paletteDock.ExitRequested += RequestExit;
            paletteDock.SettingsRequested += OpenSettings;
            paletteDock.PalettePicked += _ => NotifyTutorial(TutorialGoal.PickPalette);
            paletteDock.gameObject.SetActive(false);

            colorWheel.Build(m_Camera, m_Palettes);
            AddMovable(colorWheel.gameObject, recallable: false);
            colorWheel.Closed += ShowDock;
            colorWheel.gameObject.SetActive(false);

            shoppingList.Build(m_Camera, m_Palettes, room);
            AddMovable(shoppingList.gameObject, recallable: false);
            shoppingList.AddRequested += (kind, variant) => room.Add(kind, variant, m_Camera.transform, m_Floor.FloorY);
            shoppingList.PickUpRequested += PickUpFromList;
            shoppingList.AddRequested += (kind, _) => NotifyTutorial(TutorialGoal.AddPiece, kind.ToString());
            shoppingList.PickUpRequested += (kind, _, __) => NotifyTutorial(TutorialGoal.AddPiece, kind.ToString());
            shoppingList.Closed += ShowDock;
            shoppingList.Closed += () => NotifyTutorial(TutorialGoal.CloseShoppingList);
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
            BuildTutorialAndExit();
            m_Floor.FloorChanged += OnFloorChanged;

            // The room in progress is saved whenever it changes, so it can be continued next time.
            room.Changed += QueueRoomSave;
            room.Changed += UpdateRadio; // the music plays from a radio in the room
            m_Palettes.PaletteChanged += _ => QueueRoomSave();
            m_Palettes.CustomPaletteChanged += _ => NotifyTutorial(TutorialGoal.SavePalette);
            FurnitureClickDetector.Moved += OnFurnitureMoved;
        }

        // ---- tutorial, exit, saving the room -------------------------------------------------------

        const float RoomSaveDelay = 2f;

        TutorialPanel m_Tutorial;
        MovablePanel m_TutorialPanel;
        TutorialPointer m_Pointer;
        ConfirmDialog m_Confirm;
        SettingsPanel m_Settings;
        Coroutine m_RoomSave;

        void BuildTutorialAndExit()
        {
            m_Pointer = NewPanel<TutorialPointer>("Tutorial Pointer");
            m_Pointer.Build(m_Camera);
            m_Tutorial = NewPanel<TutorialPanel>("Tutorial Panel");
            m_Tutorial.Build(m_Camera, m_Pointer, ResolveTutorialTarget);
            m_Tutorial.StepShown += OnTutorialStep;
            m_TutorialPanel = AddMovable(m_Tutorial.gameObject, recallable: false);
            // Front-right, opposite the dock, and wide enough to clear the big panels that open straight
            // ahead (the 60 cm color wheel at 75 cm spans ±22°; this card starts at about 26°).
            m_TutorialPanel.SetRecallPose(0.9f, 40f, 0.12f);

            m_Confirm = NewPanel<ConfirmDialog>("Confirm Dialog");
            m_Confirm.Build(m_Camera);

            m_Settings = NewPanel<SettingsPanel>("Settings Panel");
            m_Settings.Build(m_Camera);
            m_Settings.Closed += OnSettingsClosed;
            m_Settings.MainMenuRequested += ReturnToMainMenu;
            m_Settings.QuitRequested += RequestExit;
            AddMovable(m_Settings.gameObject, recallable: false);
            m_Settings.gameObject.SetActive(false);

            // Background music, made in code; it fades in once it's ready and follows the Music slider.
            if (GetComponent<AmbientMusic>() == null)
                gameObject.AddComponent<AmbientMusic>();
        }

        /// <summary>The music moves into the first radio in the room, or back around the user when there's none.</summary>
        void UpdateRadio()
        {
            var music = AmbientMusic.Instance;
            if (music == null)
                return;
            Transform radio = null;
            foreach (var item in room.Items)
            {
                if (item != null && item.Kind == FurnitureKind.Radio)
                {
                    radio = item.transform;
                    break;
                }
            }
            music.SetRadio(radio);
        }

        /// <summary>
        /// Finds what a tutorial step points at, by the name in tutorial.json: a button on a panel, the dock's
        /// grab bar, a shopping-list tile's + button, or a piece in the room. Null while it isn't on show.
        /// </summary>
        Component ResolveTutorialTarget(string target)
        {
            switch (target)
            {
                case "tutorial.next": return m_Tutorial.NextButton;
                case "dock.bar": return paletteDock.gameObject.activeInHierarchy ? m_DockPanel.Bar : null;
                case "dock.shopping": return DockButton("Shopping List");
                case "dock.wheel": return DockButton("Color Wheel");
                case "dock.briefs": return DockButton("Design Briefs");
                case "dock.lookbook": return DockButton("Lookbook");
                case "dock.settings": return DockButton("Settings");
                case "dock.palette": return TutorialPointer.FindActive(paletteDock.transform, "Card");
                case "shop.close": return shoppingList.gameObject.activeInHierarchy ? shoppingList.CloseButton : null;
            }
            const string add = "shop.add:", piecePrefix = "item:";
            if (target.StartsWith(add) && System.Enum.TryParse(target.Substring(add.Length), out FurnitureKind kind))
            {
                // With the list closed, point at the button that opens it.
                return shoppingList.gameObject.activeInHierarchy ? shoppingList.FocusOn(kind) : DockButton("Shopping List");
            }
            if (target.StartsWith(piecePrefix) && System.Enum.TryParse(target.Substring(piecePrefix.Length), out FurnitureKind pieceKind))
            {
                FurnitureItem any = null;
                for (int i = room.Items.Count - 1; i >= 0; i--)
                {
                    var piece = room.Items[i];
                    if (piece == null)
                        continue;
                    if (piece.Kind == pieceKind)
                        return piece; // the newest one
                    if (any == null)
                        any = piece;
                }
                return any; // that piece was never added: any piece teaches the same thing
            }
            return null;
        }

        RectTransform DockButton(string name) => TutorialPointer.FindActive(paletteDock.transform, name) as RectTransform;

        /// <summary>A step about the dock brings the dock back if a big panel had put it away.</summary>
        void OnTutorialStep(string target)
        {
            if (target.StartsWith("dock."))
                ShowDock();
        }

        /// <summary>Back to the welcome panel from Settings. The room stays as it is, saved as "Continue last room".</summary>
        void ReturnToMainMenu()
        {
            m_Confirm.Cancel();
            if (m_Tutorial.Running)
                m_Tutorial.End(completed: false);
            m_Tracker.End();
            HideFloatingPanels();
            m_ObjectColor.Close();
            m_SendList.gameObject.SetActive(false);
            m_MatchBanner.gameObject.SetActive(false);
            SaveRoomNow();
            welcomePanel.Show();
            CheckBoundary();
            Debug.Log("[PaletteRoom] Back to the main menu.");
        }

        bool m_WelcomeBehindSettings;

        /// <summary>
        /// Settings opens in front of the user. Opened from the welcome panel it takes that panel's place
        /// (two panels stacked in depth are hard to read) and the welcome panel returns when it closes.
        /// </summary>
        void OpenSettings()
        {
            m_Confirm.Cancel();
            m_WelcomeBehindSettings = welcomePanel.gameObject.activeSelf;
            if (m_WelcomeBehindSettings)
                welcomePanel.gameObject.SetActive(false);
            m_Settings.ShowMainMenu(!m_WelcomeBehindSettings);
            m_Settings.Open();
            PanelSpace.MakeRoomFor(m_Settings.transform, m_Camera.transform);
        }

        void OnSettingsClosed()
        {
            if (m_WelcomeBehindSettings)
                welcomePanel.gameObject.SetActive(true);
            m_WelcomeBehindSettings = false;
        }

        /// <summary>Reads the headset's boundary; a stationary one gets the welcome panel's room-scale notice.</summary>
        void CheckBoundary()
        {
            if (TestModeActive)
                return;
            welcomePanel.SetBoundary(RoomScale.Check(out _));
        }

        void NotifyTutorial(TutorialGoal goal, string detail = null)
        {
            if (m_Tutorial != null)
                m_Tutorial.Notify(goal, detail);
        }

        /// <summary>
        /// Runs the tour in the room as it is. On a first visit that's an empty room, and the tour furnishes
        /// it one piece at a time from the shopping list.
        /// </summary>
        void StartTutorial()
        {
            m_Confirm.Cancel();
            m_Settings.gameObject.SetActive(false);
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
            m_Tutorial.Begin();
            m_TutorialPanel.PlaceInFront();
        }

        bool RoomIsEmpty()
        {
            foreach (var item in room.Items)
                if (item != null)
                    return false;
            return true;
        }

        void ContinueLastRoom()
        {
            var last = SaveData.LastRoom;
            if (last == null)
                return;
            if (RestoreFromLookbook(last))
                Debug.Log("[PaletteRoom] Continued the room from last time.");
        }

        void RequestExit()
        {
            string body = RoomIsEmpty()
                ? "Are you sure you want to leave? Your palettes, color wheel colors and lookbook are saved on the headset."
                : "Are you sure you want to leave? Your palettes, color wheel colors and lookbook are saved, and this room will be waiting under Continue last room.";
            m_Confirm.Show("Exit Palette Room?", body, "Exit", ExitExperience);
        }

        void ExitExperience()
        {
            SaveRoomNow();
            Debug.Log("[PaletteRoom] Exiting.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnFurnitureMoved(FurnitureItem item)
        {
            QueueRoomSave();
            NotifyTutorial(TutorialGoal.MoveFurniture);
        }

        void QueueRoomSave()
        {
            if (m_RoomSave != null)
                StopCoroutine(m_RoomSave);
            m_RoomSave = StartCoroutine(SaveRoomSoon());
        }

        // Waiting lets drop-in animations finish and folds a burst of changes into one write.
        IEnumerator SaveRoomSoon()
        {
            yield return new WaitForSecondsRealtime(RoomSaveDelay);
            m_RoomSave = null;
            SaveRoomNow();
        }

        void SaveRoomNow()
        {
            if (m_RoomSave != null)
            {
                StopCoroutine(m_RoomSave);
                m_RoomSave = null;
            }
            // An empty room isn't saved, so clearing the room (or a fresh start) keeps last time's room.
            if (room == null || m_Camera == null || m_Palettes == null || RoomIsEmpty())
                return;
            var palette = m_Palettes.Current;
            SaveData.SaveLastRoom(new Lookbook.Entry
            {
                title = "Last room",
                date = System.DateTime.Now.ToString("MMM d, h:mm tt"),
                paletteName = palette.name,
                paletteHex = Lookbook.PaletteHex(palette),
                pieces = Lookbook.Capture(room.Items, m_Camera.transform.position, out _, out _),
            });
        }

        // The headset going to the home screen or to sleep pauses the app; it may never come back.
        // Coming back (perhaps from setting up a boundary in the PICO settings) reads the boundary again.
        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveRoomNow();
                AudioLevels.Save();
            }
            else if (welcomePanel != null && welcomePanel.gameObject.activeSelf)
            {
                CheckBoundary();
            }
        }

        void OnApplicationQuit()
        {
            SaveRoomNow();
            AudioLevels.Save();
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
            trackerMove.SetRecallPose(0.75f, 40f, 0.15f); // front-right, opposite the dock, clear of the big panels
            m_Tracker.RevealRequested += () => m_Reveal.Play(m_Tracker.Brief, m_Tracker.Stars);
            m_Tracker.ExitRequested += m_Tracker.End;

            m_Lookbook = NewPanel<LookbookPanel>("Lookbook Panel");
            m_Lookbook.Build(m_Camera);
            AddMovable(m_Lookbook.gameObject, recallable: false);
            m_Lookbook.gameObject.SetActive(false);
            m_Lookbook.Closed += ShowDock;
            m_Lookbook.RestoreRequested += entry => RestoreFromLookbook(entry);

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

            paletteDock.BriefsRequested += () =>
            {
                HideFloatingPanels();
                m_Briefs.Open(m_Camera.transform);
                PanelSpace.MakeRoomFor(m_Briefs.transform, m_Camera.transform);
            };
            paletteDock.LookbookRequested += () =>
            {
                HideFloatingPanels();
                m_Lookbook.Open(m_Camera.transform, canReveal: !RoomIsEmpty());
                PanelSpace.MakeRoomFor(m_Lookbook.transform, m_Camera.transform);
            };
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
            yield return m_Tutorial.gameObject;
            yield return m_Confirm.gameObject;
            yield return m_Settings.gameObject;
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

        /// <summary>
        /// Lays a saved design out if it fits this room. If it doesn't, the room is left as it is and the
        /// user is told why; from the welcome panel they're offered an empty room instead.
        /// </summary>
        bool RestoreFromLookbook(Lookbook.Entry entry)
        {
            if (!room.Restore(entry, m_Camera.transform, m_Floor.FloorY, out var problem))
            {
                bool fromWelcome = welcomePanel.gameObject.activeSelf;
                m_Confirm.Show("This room doesn't fit here",
                    Loc.F("\"{0}\" was made for a different space. {1}", Loc.T(entry.title),
                        Loc.IsEnglish ? problem : Loc.T("It doesn't fit around the walls and furniture here.")),
                    fromWelcome ? "Start a new room" : "OK",
                    fromWelcome ? (System.Action)StartMyRoom : null);
                return false;
            }
            m_Lookbook.gameObject.SetActive(false);
            m_SendList.gameObject.SetActive(false);
            m_Tracker.End();
            welcomePanel.Hide();
            var palette = Lookbook.PaletteOf(entry);
            if (palette != null)
                m_Palettes.SetPalette(palette);
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
            return true;
        }

        void OnDestroy()
        {
            FurnitureClickDetector.Clicked -= OpenObjectColor;
            FurnitureClickDetector.Moved -= OnFurnitureMoved;
        }

        void OpenObjectColor(FurnitureItem item)
        {
            if (item == null || welcomePanel.gameObject.activeSelf)
                return;
            m_ObjectColor.Open(item);
            var toPanel = m_ObjectColor.transform.position - m_Camera.transform.position;
            PanelSpace.KeepClear(m_ObjectColor.transform, m_Camera.transform, Vector3.Dot(toPanel, m_Camera.transform.right));
            NotifyTutorial(TutorialGoal.ClickFurniture);
        }

        IEnumerator Start()
        {
            if (TestModeActive)
            {
                m_Camera.gameObject.AddComponent<EditorTestCamera>();
                m_Floor.UseSimulatedFloor();
                yield return null; // let the camera stand up before placing panels
                if (autoStartSampleRoom)
                {
                    welcomePanel.Show();
                    StartSampleRoom();
                }
                else
                {
                    yield return PlayIntro();
                    welcomePanel.Show();
                }
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
            yield return PlayIntro();
            // The opening is just the welcome panel; the room-color match waits until a fresh room is started.
            welcomePanel.Show();
            CheckBoundary(); // room-scale: a stationary boundary gets a prompt to set up a custom one

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

        /// <summary>The launch intro: the palette's swatches and the title, about four seconds.</summary>
        IEnumerator PlayIntro()
        {
            var intro = NewPanel<IntroSequence>("Intro");
            yield return intro.Play(m_Camera);
        }

        // The room's colors change as the result appears, not before the spinner has had its moment.
        void OnRoomColorsMatched(RoomColorScanner.Match match) =>
            m_MatchBanner.Show(match, () => m_Palettes.SetPalette(match.palette));

        /// <summary>A furnished sample room to try palettes on straight away.</summary>
        void StartSampleRoom()
        {
            room.Clear();
            room.SpawnSampleRoom(m_Camera.transform, m_Floor.FloorY);
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
        }

        /// <summary>The user's own room as it is: nothing is placed, the dock opens and pieces come from the shopping list.</summary>
        void StartMyRoom()
        {
            room.Clear(); // from the main menu, a new room starts empty (the last one stays saved)
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
            // A fresh room: read its real colors and suggest the palette that suits them best.
            if (!TestModeActive)
            {
                m_MatchBanner.ShowScanning();
                m_ColorScanner.Scan();
            }
            Debug.Log("[PaletteRoom] Started in the user's own room.");
        }

        MovablePanel AddMovable(GameObject panel, bool recallable)
        {
            var movable = panel.AddComponent<MovablePanel>();
            movable.Setup(m_Camera.transform, m_Camera, recallable);
            movable.Grabbed += () => NotifyTutorial(TutorialGoal.MovePanel);
            return movable;
        }

        // One floating panel at a time keeps the view of the room clear. The dock sits closer to the
        // user than the big panels, so it steps aside while one is open and returns when it closes.
        void OpenColorWheel()
        {
            paletteDock.gameObject.SetActive(false);
            shoppingList.gameObject.SetActive(false);
            colorWheel.Open();
            PanelSpace.MakeRoomFor(colorWheel.transform, m_Camera.transform);
        }

        void OpenShoppingList()
        {
            paletteDock.gameObject.SetActive(false);
            colorWheel.gameObject.SetActive(false);
            shoppingList.Open();
            PanelSpace.MakeRoomFor(shoppingList.transform, m_Camera.transform);
            NotifyTutorial(TutorialGoal.OpenShoppingList);
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
            var input = eventSystem.GetComponent<XRUIInputModule>();
            if (input == null)
                input = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            input.trackedDeviceDragThresholdMultiplier = PinchDragThreshold;
        }

        // Pinching wobbles the aim ray more than XRI's default allows, and a press on anything in a scrolling
        // list then turns into a scroll and is lost. About 3 cm on a panel at arm's length.
        const float PinchDragThreshold = 6f;
        const float PokeDepth = 0.03f, NearGrabRadius = 0.03f;

        /// <summary>
        /// A poke or near grab in range takes over from the hand's ray and drops any press it was making.
        /// At the rig's defaults that range is 10 cm, so just reaching towards a panel lost presses; now the
        /// hand has to really touch the panel or piece.
        /// </summary>
        static void TightenHandReach()
        {
            foreach (var poke in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                poke.pokeDepth = PokeDepth;
            foreach (var nearFar in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (nearFar.nearInteractionCaster is SphereInteractionCaster near)
                    near.castRadius = NearGrabRadius;
            }
        }
    }
}
