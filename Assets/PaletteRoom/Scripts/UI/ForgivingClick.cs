using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Makes a button easy to press with hand tracking. A uGUI click only counts when the pinch is released
    /// over the button it started on, but the hand's aim ray dips as the fingers close, so on small or
    /// short targets (swatches, slim buttons) the release often lands just off it and the click is lost.
    /// A quick press-and-release still clicks here as long as it wasn't a scroll and didn't end on a
    /// different control. Buttons without a tint transition (color swatches) also grow a little on hover,
    /// so the user can see what they are pointing at.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ForgivingClick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        const float MaxClickSeconds = 0.6f;
        const float HoverScale = 1.08f;

        Button m_Button;
        float m_PressedAt = float.NegativeInfinity;
        Vector3 m_RestScale;

        void Awake()
        {
            m_Button = GetComponent<Button>();
            m_RestScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData) => m_PressedAt = Time.unscaledTime;

        public void OnPointerUp(PointerEventData eventData)
        {
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_Button.transition == Selectable.Transition.None && m_Button.IsInteractable())
                transform.localScale = m_RestScale * HoverScale;
        }

        public void OnPointerExit(PointerEventData eventData) => transform.localScale = m_RestScale;

        void OnDisable() => transform.localScale = m_RestScale;
    }
}
