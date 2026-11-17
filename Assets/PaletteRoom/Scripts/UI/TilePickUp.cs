using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Pinch a shopping-list picture and drag it out into the room: the piece lifts out of the list into
    /// your hand, follows it, and is set down where you let go. A short steady hold lifts it too. Only a
    /// mostly up-and-down swipe scrolls the list.
    /// </summary>
    public class TilePickUp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float HoldSeconds = 0.25f;
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
            if (m_Lifted || m_Press == null)
                return;
            // Dragged off the picture: lift it, unless it was an up-and-down swipe (a scroll).
            var moved = m_Press.position - m_PressedAtScreen;
            if (Mathf.Abs(moved.y) > 1.5f * Mathf.Abs(moved.x))
                m_Press = null;
            else
                Lift();
        }

        void Update()
        {
            if (m_Press == null || m_Lifted)
                return;
            var moved = m_Press.position - m_PressedAtScreen;
            bool dragged = moved.sqrMagnitude > MaxWobblePixels * MaxWobblePixels;
            if (dragged && Mathf.Abs(moved.y) > 1.5f * Mathf.Abs(moved.x) && Time.unscaledTime - m_PressedAt < HoldSeconds)
            {
                m_Press = null; // a quick up-and-down swipe: it's a scroll, not a pick-up
                return;
            }
            if (!dragged && Time.unscaledTime - m_PressedAt < HoldSeconds)
                return;
            Lift();
        }

        void Lift()
        {
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
