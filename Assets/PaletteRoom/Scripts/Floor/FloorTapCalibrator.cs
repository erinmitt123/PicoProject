using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PaletteRoom
{
    /// <summary>
    /// "Tap the floor to set height": while active, the user lowers a hand or controller to the floor
    /// and pinches / pulls the trigger. Uses the near/far interactors, so hands and controllers behave
    /// the same.
    /// </summary>
    public class FloorTapCalibrator : MonoBehaviour
    {
        [SerializeField] FloorDetector floorDetector;
        [SerializeField] Transform head;
        [Tooltip("A tap only counts this far below the head. Kept small because the head drops too when the user crouches or kneels.")]
        [SerializeField] float minDistanceBelowHead = 0.5f;
        [Tooltip("Distance from the tracked hand/controller point down to the real floor when touching it.")]
        [SerializeField] float contactOffset = 0.03f;
        [Tooltip("Ring shown under the lowest hand/controller while calibrating.")]
        [SerializeField] Transform reticle;

        readonly InteractorPoints m_Points = new InteractorPoints();

        public bool IsActive { get; private set; }

        public event Action Completed;

        public void Configure(FloorDetector detector, Transform headTransform, Transform reticleTransform)
        {
            floorDetector = detector;
            head = headTransform;
            reticle = reticleTransform;
        }

        public void Begin()
        {
            IsActive = true;
            if (reticle != null)
                reticle.gameObject.SetActive(false);
        }

        public void Cancel()
        {
            IsActive = false;
            if (reticle != null)
                reticle.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsActive || head == null || floorDetector == null)
                return;

            float ceiling = head.position.y - minDistanceBelowHead;
            bool found = false;
            Vector3 lowest = default;
            foreach (var interactor in m_Points.Active)
            {
                if (!interactor.isActiveAndEnabled)
                    continue;
                var p = InteractorPoints.Position(interactor);
                if (p.y > ceiling)
                    continue;
                // Pressing a UI button (e.g. Cancel on the dock, which follows the hand down) is not a floor tap.
                bool overUI = interactor.TryGetCurrentUIRaycastResult(out _);
                if (interactor.logicalSelectState.wasPerformedThisFrame && !overUI)
                {
                    Finish(p.y - contactOffset);
                    return;
                }
                if (!found || p.y < lowest.y)
                {
                    lowest = p;
                    found = true;
                }
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(found);
                if (found)
                    reticle.position = new Vector3(lowest.x, lowest.y - contactOffset + 0.002f, lowest.z);
            }
        }

        void Finish(float floorY)
        {
            Cancel();
            floorDetector.SetFloorFromTap(floorY);
            Completed?.Invoke();
        }
    }
}
