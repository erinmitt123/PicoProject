using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Makes a button easy to press with hand tracking. A uGUI click only counts when the pinch is released
    /// over the button it started on, but the hand's aim ray dips as the fingers close, so on small or
    /// short targets (swatches, slim buttons) the release often lands just off it and the click is lost.
    /// A quick press-and-release still clicks here as long as it wasn't a scroll and didn't end on a
    /// different control. XRI can also drop a pointer mid-pinch (the hand comes near a panel, so poke takes
    /// over) without ever sending pointer-up, which would leave the button grey and unclicked, so the pinch
    /// itself is watched too. Buttons without a tint transition (color swatches) also grow a little on
    /// hover, so the user can see what they are pointing at.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ForgivingClick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        const float MaxClickSeconds = 0.6f;
        const float HoverScale = 1.08f;
        // The input module runs in Update, so a release still unanswered after this many LateUpdates was dropped.
        const int LostReleaseFrames = 2;

        static ForgivingClick s_Held;

        Button m_Button;
        float m_PressedAt = float.NegativeInfinity;
        Vector3 m_RestScale;
        PointerEventData m_Press;
        NearFarInteractor m_PressedBy;
        int m_ReleasedFrames;

        void Awake()
        {
            m_Button = GetComponent<Button>();
            m_RestScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_PressedAt = Time.unscaledTime;
            // A dropped pointer that comes back while still pinching presses again, maybe on another button.
            if (s_Held != null && s_Held != this)
                s_Held.ReleaseLostPress();
            m_Press = eventData;
            m_PressedBy = (eventData as TrackedDeviceEventData)?.interactor as NearFarInteractor;
            m_ReleasedFrames = 0;
            s_Held = this;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ClearHeld();
            bool quick = Time.unscaledTime - m_PressedAt <= MaxClickSeconds;
            m_PressedAt = float.NegativeInfinity;
            if (!quick || eventData.dragging || !m_Button.IsInteractable())
                return;
            var over = eventData.pointerCurrentRaycast.gameObject;
            if (over != null && over.transform.IsChildOf(transform))
                return; // still on the button: its own click handles it
            if (over != null && over.GetComponentInParent<Selectable>() != null)
                return; // the user moved on to another control
            m_Button.onClick.Invoke();
        }

        void LateUpdate()
        {
            if (m_PressedBy == null)
                return;
            if (m_PressedBy.uiPressInput.ReadIsPerformed())
            {
                m_ReleasedFrames = 0;
                return;
            }
            if (++m_ReleasedFrames < LostReleaseFrames)
                return;
            bool quick = Time.unscaledTime - m_PressedAt <= MaxClickSeconds;
            ReleaseLostPress();
            if (quick && m_Button.IsInteractable())
                m_Button.onClick.Invoke();
        }

        /// <summary>The pointer-up that never came: takes the button out of its pressed state.</summary>
        void ReleaseLostPress()
        {
            var press = m_Press;
            ClearHeld();
            m_PressedAt = float.NegativeInfinity;
            if (press != null)
                m_Button.OnPointerUp(press);
        }

        void ClearHeld()
        {
            m_Press = null;
            m_PressedBy = null;
            if (s_Held == this)
                s_Held = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_Button.transition == Selectable.Transition.None && m_Button.IsInteractable())
                transform.localScale = m_RestScale * HoverScale;
        }

        public void OnPointerExit(PointerEventData eventData) => transform.localScale = m_RestScale;

        void OnDisable()
        {
            ClearHeld();
            transform.localScale = m_RestScale;
        }
    }
}
