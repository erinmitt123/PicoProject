using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Press and hold a shopping-list icon to lift that piece out of the list and into your hand; keep
    /// holding to carry it into the room and let go to set it down. A quick swipe still scrolls the list:
    /// pick-up only happens after a short, steady hold.
    /// </summary>
    public class TilePickUp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float HoldSeconds = 0.32f;
        const float MaxWobblePixels = 18f;

        /// <summary>Raised when a hold completes: the piece to lift, and the interactor that should carry it (null for the mouse).</summary>
        public event Action<IXRSelectInteractor> PickedUp;

        ScrollRect m_Scroll;
        PointerEventData m_Press;
        float m_PressedAt;
        Vector2 m_PressedAtScreen;
        bool m_Lifted;

        void Awake() => m_Scroll = GetComponentInParent<ScrollRect>();

        public void OnPointerDown(PointerEventData eventData)
        {
            m_Press = eventData;
            m_PressedAt = Time.unscaledTime;
            m_PressedAtScreen = eventData.position;
            m_Lifted = false;
        }

        public void OnPointerUp(PointerEventData eventData) => End();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!m_Lifted)
                m_Press = null;
        }

        void Update()
        {
            if (m_Press == null || m_Lifted)
                return;
            if ((m_Press.position - m_PressedAtScreen).sqrMagnitude > MaxWobblePixels * MaxWobblePixels)
            {
                m_Press = null; // it's a scroll, not a pick-up
                return;
            }
            if (Time.unscaledTime - m_PressedAt < HoldSeconds)
                return;

            m_Lifted = true;
            // Stop the list scrolling while the piece is carried away.
            if (m_Scroll != null)
            {
                m_Scroll.StopMovement();
                m_Scroll.enabled = false;
            }
            IXRSelectInteractor hand = null;
            if (m_Press is TrackedDeviceEventData tracked && tracked.interactor is IXRSelectInteractor selector)
                hand = selector;
            if (hand is XRBaseInputInteractor input)
                input.SendHapticImpulse(0.35f, 0.05f);
            PickedUp?.Invoke(hand);
        }

        void End()
        {
            m_Press = null;
            m_Lifted = false;
            if (m_Scroll != null)
                m_Scroll.enabled = true;
        }

        void OnDisable() => End();
    }
}
