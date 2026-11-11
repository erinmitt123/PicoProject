using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PaletteRoom
{
    /// <summary>
    /// Tells a click on a piece apart from a drag: a select released quickly without moving the piece
    /// counts as a click. Clicks raise <see cref="Clicked"/> (the app opens the piece's color panel).
    /// </summary>
    public class FurnitureClickDetector : MonoBehaviour
    {
        const float MaxClickSeconds = 0.45f, MaxClickMove = 0.03f;

        /// <summary>Raised when the user clicks (rather than drags) a piece.</summary>
        public static event Action<FurnitureItem> Clicked;

        /// <summary>Raised when the user drags a piece somewhere and lets go.</summary>
        public static event Action<FurnitureItem> Moved;

        XRGrabInteractable m_Grab;
        FurnitureItem m_Item;
        float m_PressedAt;
        Vector3 m_PressedPos;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Item = GetComponent<FurnitureItem>();
        }

        void OnEnable()
        {
            if (m_Grab == null)
                return;
            m_Grab.selectEntered.AddListener(OnSelectEntered);
            m_Grab.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            if (m_Grab == null)
                return;
            m_Grab.selectEntered.RemoveListener(OnSelectEntered);
            m_Grab.selectExited.RemoveListener(OnSelectExited);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_PressedAt = Time.unscaledTime;
            m_PressedPos = transform.position;
            Sfx.PlayAt(SfxKind.PickUp, transform.position, 0.8f);
            // A small tick in the controller as the piece is taken (hands have no haptics).
            (args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor)?.SendHapticImpulse(0.3f, 0.05f);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.isCanceled || m_Grab.isSelected)
                return;
            bool quick = Time.unscaledTime - m_PressedAt <= MaxClickSeconds;
            bool still = Vector3.Distance(transform.position, m_PressedPos) <= MaxClickMove;
            if (quick && still)
            {
                Sfx.Play(SfxKind.Click);
                RaiseClicked(m_Item);
            }
            else if (!still && m_Item != null)
            {
                Sfx.PlayAt(SfxKind.Drop, transform.position, 0.7f);
                (args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor)?.SendHapticImpulse(0.45f, 0.07f);
                Moved?.Invoke(m_Item);
            }
        }

        /// <summary>Also used by Editor test mode for mouse clicks.</summary>
        public static void RaiseClicked(FurnitureItem item)
        {
            if (item != null)
                Clicked?.Invoke(item);
        }
    }
}
