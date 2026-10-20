using UnityEngine;
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
            if (mouse == null || !mouse.rightButton.isPressed)
                return;
            var delta = mouse.delta.ReadValue();
            m_Yaw += delta.x * degreesPerPixel;
            m_Pitch = Mathf.Clamp(m_Pitch - delta.y * degreesPerPixel, -80f, 80f);
            transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
        }
    }
}
