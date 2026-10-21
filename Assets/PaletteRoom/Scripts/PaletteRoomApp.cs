using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Entry point: welcome panel → "Start with a sample room" → palette dock, with the color wheel and
    /// shopping list opened from the dock. Owns floor detection and keeps furniture resting on the floor.
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

            welcomePanel.Build(m_Camera, m_Floor, m_Calibrator);
            welcomePanel.StartSampleRoomRequested += StartSampleRoom;
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

            m_Floor.FloorChanged += OnFloorChanged;
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
        }

        void StartSampleRoom()
        {
            room.SpawnSampleRoom(m_Camera.transform, m_Floor.FloorY);
            welcomePanel.Hide();
            paletteDock.gameObject.SetActive(true);
            m_DockPanel.PlaceIfNeeded();
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
            if (!colorWheel.gameObject.activeSelf && !shoppingList.gameObject.activeSelf)
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
