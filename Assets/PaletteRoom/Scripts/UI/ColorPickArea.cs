using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaletteRoom
{
    /// <summary>
    /// Reports where on its rect the user is pointing while pressed: works for poke, near/far ray,
    /// controller trigger and mouse because it reads the raycast hit rather than a screen position.
    /// Positions are normalised to -1..1 across the rect (x right, y up).
    /// </summary>
    public class ColorPickArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public event Action<Vector2> Picked;

        public void OnPointerDown(PointerEventData eventData) => Report(eventData);

        public void OnDrag(PointerEventData eventData) => Report(eventData);

        // Start dragging immediately so small hand jitter doesn't get swallowed by the drag threshold.
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        void Report(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Vector2 local;
            var hit = eventData.pointerCurrentRaycast;
            if (hit.isValid && hit.worldPosition != Vector3.zero)
            {
                local = rt.InverseTransformPoint(hit.worldPosition);
            }
            else if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            var r = rt.rect;
            var normalized = new Vector2(
                (local.x - r.center.x) / (r.width * 0.5f),
                (local.y - r.center.y) / (r.height * 0.5f));
            Picked?.Invoke(normalized);
        }
    }
}
