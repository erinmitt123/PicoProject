using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PaletteRoom
{
    /// <summary>
    /// "Tap the floor to set height": while active, the user lowers a hand or controller to the floor (or
    /// points its ray at a scanned floor surface) and pinches / pulls the trigger. Uses the near/far
    /// interactors, so hands and controllers behave the same.
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
        SceneObstacles scene;

        void Awake() => scene = GetComponent<SceneObstacles>();

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
            bool found = false, pointed = false;
            Vector3 lowest = default, aimed = default;
            foreach (var interactor in m_Points.Active)
            {
                if (!interactor.isActiveAndEnabled)
                    continue;
                // Pressing a UI button (e.g. Cancel on the dock, which follows the hand down) is not a floor tap.
                bool overUI = interactor.TryGetCurrentUIRaycastResult(out _);
                bool pressed = interactor.logicalSelectState.wasPerformedThisFrame && !overUI;
                var p = InteractorPoints.Position(interactor);
                if (p.y <= ceiling)
                {
                    if (pressed)
                    {
                        Finish(p.y - contactOffset);
                        return;
                    }
                    if (!found || p.y < lowest.y)
                    {
                        lowest = p;
                        found = true;
                    }
                    continue;
                }

                // Out of reach of the floor: pointing the ray at a scanned floor surface works too.
                if (!overUI && TryPointAtFloor(interactor, out var hit))
                {
                    if (pressed)
                    {
                        Finish(hit.y);
                        return;
                    }
                    aimed = hit;
                    pointed = true;
                }
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(found || pointed);
                if (found)
                    reticle.position = new Vector3(lowest.x, lowest.y - contactOffset + 0.002f, lowest.z);
                else if (pointed)
                    reticle.position = aimed + Vector3.up * 0.002f;
            }
        }

        /// <summary>Where the interactor's far ray meets a scanned floor surface, if it does.</summary>
        bool TryPointAtFloor(NearFarInteractor interactor, out Vector3 hit)
        {
            hit = default;
            var caster = interactor.farInteractionCaster != null ? interactor.farInteractionCaster.castOrigin : null;
            if (caster == null || scene == null)
                return false;
            var ray = new Ray(caster.position, caster.forward);
            if (ray.direction.y > -0.05f)
                return false;
            float best = float.MaxValue;
            foreach (var s in scene.Surfaces)
            {
                if (s.kind != SurfaceKind.Floor)
                    continue;
                float t = (s.centre.y - ray.origin.y) / ray.direction.y;
                if (t <= 0f || t >= best || t > 10f)
                    continue;
                var point = ray.GetPoint(t);
                var local = Quaternion.Inverse(s.rotation) * (point - s.centre);
                if (Mathf.Abs(local.x) > s.size.x / 2f || Mathf.Abs(local.y) > s.size.y / 2f)
                    continue;
                best = t;
                hit = point;
            }
            return best < float.MaxValue;
        }

        void Finish(float floorY)
        {
            Cancel();
            Debug.Log($"[PaletteRoom] Floor tapped at y={floorY:F3}.");
            floorDetector.SetFloorFromTap(floorY);
            Completed?.Invoke();
        }
    }
}
