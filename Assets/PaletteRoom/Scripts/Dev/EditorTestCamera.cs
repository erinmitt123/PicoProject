using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PaletteRoom
{
    /// <summary>
    /// Editor test mode only: hold the right mouse button and drag to look around, since there is no
    /// headset to turn. Added at runtime by <see cref="PaletteRoomApp"/>; never present in device builds.
    /// </summary>
    public class EditorTestCamera : MonoBehaviour
    {
        [SerializeField] float degreesPerPixel = 0.15f;
        [SerializeField] float eyeHeight = 1.6f;

        float m_Yaw, m_Pitch;

        void Start()
        {
            // Without tracking the camera sits at the rig's default offset; stand the user up.
            var p = transform.position;
            if (p.y < eyeHeight - 0.05f)
                transform.position = new Vector3(p.x, eyeHeight, p.z);
            var e = transform.eulerAngles;
            m_Yaw = e.y;
            m_Pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            if (mouse.leftButton.wasPressedThisFrame)
                ClickFurniture(mouse.position.ReadValue());
            if (!mouse.rightButton.isPressed)
                return;
            var delta = mouse.delta.ReadValue();
            m_Yaw += delta.x * degreesPerPixel;
            m_Pitch = Mathf.Clamp(m_Pitch - delta.y * degreesPerPixel, -80f, 80f);
            transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
        }

        /// <summary>Left-click a piece (not through a panel) to open its color panel, like a quick trigger press.</summary>
        void ClickFurniture(Vector2 screenPoint)
        {
            var cam = GetComponent<Camera>();
            if (cam == null)
                return;
            var ray = cam.ScreenPointToRay(screenPoint);
            float nearest = float.MaxValue;
            FurnitureItem item = null;
            foreach (var hit in Physics.RaycastAll(ray, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                // Panels sit in front of furniture; a click on one is meant for the panel.
                item = hit.collider.GetComponentInParent<MovablePanel>() != null ? null : hit.collider.GetComponentInParent<FurnitureItem>();
            }
            if (item != null && !OverUI(screenPoint))
                FurnitureClickDetector.RaiseClicked(item);
        }

        static readonly List<RaycastResult> s_UIHits = new List<RaycastResult>();

        static bool OverUI(Vector2 screenPoint)
        {
            var events = EventSystem.current;
            if (events == null)
                return false;
            s_UIHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPoint }, s_UIHits);
            return s_UIHits.Count > 0;
        }
    }
}
