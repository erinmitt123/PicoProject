using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// First thing the user sees: what the app does, floor status, and "Start with a sample room".
    /// Spawns in front of the user at a comfortable height.
    /// </summary>
    public class WelcomePanel : MonoBehaviour
    {
        const float Width = 520f, Height = 430f, Pad = 32f;

        [SerializeField] float distance = 1.1f;
        [SerializeField] float belowEyes = 0.15f;

        Canvas m_Canvas;
        TextMeshProUGUI m_FloorStatus;
        Image m_FloorDot;
        TextMeshProUGUI m_Body;
        Button m_StartButton;
        Button m_FloorButton;
        TextMeshProUGUI m_FloorButtonLabel;
        FloorDetector m_Floor;
        FloorTapCalibrator m_Calibrator;
        Transform m_Head;

        public event Action StartSampleRoomRequested;

        public void Build(Camera eventCamera, FloorDetector floor, FloorTapCalibrator calibrator)
        {
            m_Head = eventCamera.transform;
            m_Floor = floor;
            m_Calibrator = calibrator;

            m_Canvas = UIKit.CreateWorldCanvas("Welcome Canvas", new Vector2(Width, Height), transform, eventCamera);
            var root = m_Canvas.transform;

            var panel = UIKit.Panel(root, "Panel", UIKit.PanelColor, 28f);
            UIKit.Stretch(panel.rectTransform);

            var title = UIKit.Label(panel.transform, "Title", "Palette Room", 46f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(title.rectTransform, Pad, Pad, Width - 2 * Pad, 56f);

            m_Body = UIKit.Label(panel.transform, "Body",
                "Restyle your real room in color. Drop true-scale furniture onto your floor, pick a palette, and watch every piece update using the 60-30-10 design rule.",
                21f, UIKit.MutedTextColor);
            UIKit.Place(m_Body.rectTransform, Pad, Pad + 66f, Width - 2 * Pad, 100f);

            m_FloorDot = UIKit.Panel(panel.transform, "Floor Dot", Color.gray, 7f);
            UIKit.Place(m_FloorDot.rectTransform, Pad, Pad + 183f, 14f, 14f);
            m_FloorStatus = UIKit.Label(panel.transform, "Floor Status", string.Empty, 19f, UIKit.TextColor);
            UIKit.Place(m_FloorStatus.rectTransform, Pad + 24f, Pad + 176f, Width - 2 * Pad - 24f, 52f);

            m_StartButton = UIKit.Button(panel.transform, "Start Sample Room", "Start with a sample room", 24f, true, OnStartPressed);
            UIKit.Place((RectTransform)m_StartButton.transform, Pad, Height - Pad - 134f, Width - 2 * Pad, 70f);

            m_FloorButton = UIKit.Button(panel.transform, "Set Floor", "Set floor height", 19f, false, OnFloorButtonPressed);
            UIKit.Place((RectTransform)m_FloorButton.transform, Pad, Height - Pad - 56f, Width - 2 * Pad, 50f);
            m_FloorButtonLabel = m_FloorButton.GetComponentInChildren<TextMeshProUGUI>();

            m_Floor.FloorChanged += _ => Refresh();
            m_Calibrator.Completed += Refresh;
            Refresh();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            PlaceInFront();
            Refresh();
        }

        public void Hide()
        {
            if (m_Calibrator != null && m_Calibrator.IsActive)
                m_Calibrator.Cancel();
            gameObject.SetActive(false);
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

        void OnStartPressed()
        {
            StartSampleRoomRequested?.Invoke();
        }

        void OnFloorButtonPressed()
        {
            if (m_Calibrator.IsActive)
                m_Calibrator.Cancel();
            else
                m_Calibrator.Begin();
            Refresh();
        }

        void Refresh()
        {
            if (m_Floor == null || m_FloorStatus == null)
                return;

            if (m_Calibrator.IsActive)
            {
                m_FloorDot.color = new Color(0.95f, 0.65f, 0.2f);
                m_FloorStatus.text = "Touch the floor with a hand or controller, or point at it, then pinch or pull the trigger.";
                m_FloorButtonLabel.text = "Cancel";
                m_StartButton.interactable = false;
                return;
            }

            m_StartButton.interactable = true;
            m_FloorButtonLabel.text = "Set floor height";
            switch (m_Floor.State)
            {
                case FloorState.Searching:
                    m_FloorDot.color = new Color(0.6f, 0.6f, 0.62f);
                    m_FloorStatus.text = "Looking for your floor…";
                    break;
                case FloorState.Detected:
                    m_FloorDot.color = new Color(0.36f, 0.7f, 0.45f);
                    m_FloorStatus.text = "Floor detected";
                    break;
                case FloorState.NotFound:
                    m_FloorDot.color = new Color(0.95f, 0.65f, 0.2f);
                    m_FloorStatus.text = "Couldn't detect the floor. Tap it to set the height.";
                    break;
                case FloorState.SetByTap:
                    m_FloorDot.color = new Color(0.36f, 0.7f, 0.45f);
                    m_FloorStatus.text = "Floor height set";
                    break;
                case FloorState.Simulated:
                    m_FloorDot.color = new Color(0.4f, 0.55f, 0.85f);
                    m_FloorStatus.text = "Editor test mode: using the tracking floor";
                    break;
            }
        }
    }
}
