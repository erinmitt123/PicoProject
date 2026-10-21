using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
using UnityEngine.XR;
#endif

namespace PaletteRoom
{
    public enum FloorState
    {
        /// <summary>Waiting for plane detection; the tracking-origin floor is used meanwhile.</summary>
        Searching,
        /// <summary>A floor plane was detected.</summary>
        Detected,
        /// <summary>Plane detection found nothing in time; the user can tap the floor to set it.</summary>
        NotFound,
        /// <summary>The user set the floor height by tapping it.</summary>
        SetByTap,
        /// <summary>Editor test mode without a headset: the tracking-origin floor is used as-is.</summary>
        Simulated,
    }

    /// <summary>
    /// Finds the real floor height. Uses PICO plane detection when available and falls back to the
    /// tracking-origin floor plus a "tap the floor" calibration (see <see cref="FloorTapCalibrator"/>).
    /// </summary>
    public class FloorDetector : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [Tooltip("Seconds to wait for a floor plane before offering the tap-to-set fallback.")]
        [SerializeField] float detectionTimeout = 8f;
        [Tooltip("Horizontal planes smaller than this (m²) are ignored, e.g. table tops.")]
        [SerializeField] float minFloorArea = 0.5f;
        [Tooltip("Optional: plane visualisation to hide once the floor is known.")]
        [SerializeField] GameObject planeVisualization;

        readonly Dictionary<Guid, float> m_FloorCandidates = new Dictionary<Guid, float>();
        float m_SearchStarted;
        float m_FloorY;

        public FloorState State { get; private set; } = FloorState.Searching;
        public float FloorY => m_FloorY;

        /// <summary>How far scanned surfaces must move vertically to line up with the floor the user sees.</summary>
        public float ScanOffsetY { get; private set; }

        /// <summary>Raised whenever <see cref="State"/> or <see cref="FloorY"/> changes.</summary>
        public event Action<FloorDetector> FloorChanged;

        void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();
            // Tracking origin is floor-level on PICO, so it is a sensible starting guess.
            m_FloorY = xrOrigin != null ? xrOrigin.transform.position.y : 0f;
        }

        void OnEnable()
        {
            m_SearchStarted = Time.time;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            PXR_Manager.PlaneDetectionDataUpdated += OnPlanesUpdated;
            StartPlaneProvider();
#endif
        }

        void OnDisable()
        {
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            PXR_Manager.PlaneDetectionDataUpdated -= OnPlanesUpdated;
#endif
        }

        void Update()
        {
            if (State == FloorState.Searching && Time.time - m_SearchStarted > detectionTimeout)
                SetState(FloorState.NotFound, m_FloorY);
        }

        /// <summary>Sets the floor from a tap. Always wins over later plane updates.</summary>
        public void SetFloorFromTap(float worldY)
        {
            SetState(FloorState.SetByTap, worldY);
        }

        /// <summary>Skips detection for Editor testing: the floor is the tracking origin.</summary>
        public void UseSimulatedFloor()
        {
            SetState(FloorState.Simulated, xrOrigin != null ? xrOrigin.transform.position.y : 0f);
        }

        void SetState(FloorState state, float floorY)
        {
            bool changed = state != State || !Mathf.Approximately(floorY, m_FloorY);
            State = state;
            m_FloorY = floorY;
            if (state == FloorState.Detected || state == FloorState.SetByTap || state == FloorState.Simulated)
            {
                if (planeVisualization != null)
                    planeVisualization.SetActive(false);
            }
            if (changed)
                FloorChanged?.Invoke(this);
        }

#if ENABLE_PICO_XR_SDK
        async void StartPlaneProvider()
        {
            // Requests the spatial-data permission on device; the PlaneDetectionManager from the
            // plane building block may already have started it, which is harmless.
            var result = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);
            if (result != PxrResult.SUCCESS)
                Debug.LogWarning($"[PaletteRoom] Plane detection unavailable ({result}); using tap-to-set floor.");
        }

        void OnPlanesUpdated(List<PxrPlaneData> planes)
        {
            if (State == FloorState.SetByTap || State == FloorState.Simulated)
                return;

            foreach (var plane in planes)
            {
                if (plane.state == MeshChangeState.Removed)
                {
                    m_FloorCandidates.Remove(plane.uuid);
                    continue;
                }
                if (plane.state == MeshChangeState.Unchanged)
                    continue;

                bool upward = plane.orientationMode == PxrPlaneOrientation.HorizontalUpward;
                bool labelledFloor = plane.label == PxrSemanticLabel.Floor;
                float area = plane.box2D.extent.width * plane.box2D.extent.height;
                if (labelledFloor || (upward && area >= minFloorArea))
                    m_FloorCandidates[plane.uuid] = TrackingToWorld(plane.position).y;
                else
                    m_FloorCandidates.Remove(plane.uuid);
            }

            if (m_FloorCandidates.Count == 0)
                return;

            // The floor is the lowest large upward-facing surface.
            float lowest = float.MaxValue;
            foreach (var y in m_FloorCandidates.Values)
                lowest = Mathf.Min(lowest, y);

            // With a floor-level tracking origin the runtime already knows the floor height (from the
            // boundary set-up), and that is what the user sees as the floor. The scan can sit at a
            // constant offset from it (0.40 m in the PICO Emulator), so the tracking floor wins and the
            // difference is reported so scanned surfaces can be shifted to match.
            if (xrOrigin != null && xrOrigin.CurrentTrackingOriginMode == UnityEngine.XR.TrackingOriginModeFlags.Floor)
            {
                float tracked = xrOrigin.transform.position.y;
                float offset = tracked - lowest;
                bool offsetChanged = Mathf.Abs(offset - ScanOffsetY) > 0.01f;
                if (offsetChanged)
                {
                    ScanOffsetY = Mathf.Abs(offset) > 0.03f ? offset : 0f;
                    Debug.Log($"[PaletteRoom] Scanned floor at y={lowest:F3}, tracking floor at y={tracked:F3}; shifting the scan by {ScanOffsetY:F3} m.");
                }
                bool stateChanged = State != FloorState.Detected || !Mathf.Approximately(tracked, m_FloorY);
                SetState(FloorState.Detected, tracked);
                if (offsetChanged && !stateChanged)
                    FloorChanged?.Invoke(this); // the floor didn't move, but the scan has to
                return;
            }
            SetState(FloorState.Detected, lowest);
        }

        Vector3 TrackingToWorld(Vector3 trackingPosition)
        {
            var space = xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null
                ? xrOrigin.CameraFloorOffsetObject.transform
                : null;
            return space != null ? space.TransformPoint(trackingPosition) : trackingPosition;
        }
#endif
    }
}
