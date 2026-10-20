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
    /// <summary>
    /// The real room as obstacles, from PICO plane detection (in the emulator, its simulated bedroom).
    /// Walls, doors and windows become thin barriers nothing on the floor may cross; raised horizontal
    /// surfaces (bed, table, sofa, desk…) become footprints that floor-standing furniture must avoid.
    /// Virtual pieces are then placed around the real ones.
    /// </summary>
    public class SceneObstacles : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [SerializeField] FloorDetector floor;
        [Tooltip("Horizontal surfaces at least this far above the floor count as real furniture.")]
        [SerializeField] float minObstacleHeight = 0.12f;
        [Tooltip("Thickness given to walls when testing overlap (m).")]
        [SerializeField] float wallThickness = 0.06f;

        readonly Dictionary<Guid, Footprint> m_Walls = new Dictionary<Guid, Footprint>();
        readonly Dictionary<Guid, Footprint> m_Furniture = new Dictionary<Guid, Footprint>();

        public IEnumerable<Footprint> Walls => m_Walls.Values;
        public IEnumerable<Footprint> Furniture => m_Furniture.Values;
        public int WallCount => m_Walls.Count;
        public int FurnitureCount => m_Furniture.Count;

        /// <summary>Raised when the set of real-room obstacles changes.</summary>
        public event Action Changed;

        void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();
            if (floor == null)
                floor = GetComponent<FloorDetector>();
        }

#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
        void OnEnable() => PXR_Manager.PlaneDetectionDataUpdated += OnPlanesUpdated;

        void OnDisable() => PXR_Manager.PlaneDetectionDataUpdated -= OnPlanesUpdated;
#endif

#if ENABLE_PICO_XR_SDK
        void OnPlanesUpdated(List<PxrPlaneData> planes)
        {
            bool changed = false;
            float floorY = floor != null ? floor.FloorY : 0f;
            foreach (var plane in planes)
            {
                if (plane.state == MeshChangeState.Unchanged)
                    continue;
                changed |= m_Walls.Remove(plane.uuid) | m_Furniture.Remove(plane.uuid);
                if (plane.state == MeshChangeState.Removed || plane.vertices == null || plane.vertices.Length < 3)
                    continue;

                var world = new Vector3[plane.vertices.Length];
                for (int i = 0; i < world.Length; i++)
                    world[i] = TrackingToWorld(plane.position + plane.rotation * plane.vertices[i]);

                if (IsWall(plane))
                {
                    m_Walls[plane.uuid] = WallFootprint(world);
                    changed = true;
                }
                else if (plane.orientationMode == PxrPlaneOrientation.HorizontalUpward && plane.label != PxrSemanticLabel.Floor
                    && TrackingToWorld(plane.position).y - floorY >= minObstacleHeight)
                {
                    m_Furniture[plane.uuid] = SurfaceFootprint(world, TrackingRotationToWorld(plane.rotation));
                    changed = true;
                }
            }
            if (changed)
                Changed?.Invoke();
        }

        static bool IsWall(PxrPlaneData plane) =>
            plane.label == PxrSemanticLabel.Wall || plane.label == PxrSemanticLabel.Door
            || plane.label == PxrSemanticLabel.Window || plane.label == PxrSemanticLabel.Opening
            || (plane.orientationMode == PxrPlaneOrientation.Vertical && plane.label == PxrSemanticLabel.Unknown);

        Vector3 TrackingToWorld(Vector3 p)
        {
            var space = xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform : null;
            return space != null ? space.TransformPoint(p) : p;
        }

        Quaternion TrackingRotationToWorld(Quaternion r)
        {
            var space = xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform : null;
            return space != null ? space.rotation * r : r;
        }
#endif

        /// <summary>A wall as a thin strip along its widest horizontal extent.</summary>
        Footprint WallFootprint(Vector3[] world)
        {
            // The two points farthest apart in plan view are the wall's ends.
            Vector2 a = Flat(world[0]), b = a;
            float best = -1f;
            for (int i = 0; i < world.Length; i++)
            {
                for (int j = i + 1; j < world.Length; j++)
                {
                    float d = (Flat(world[i]) - Flat(world[j])).sqrMagnitude;
                    if (d > best)
                    {
                        best = d;
                        a = Flat(world[i]);
                        b = Flat(world[j]);
                    }
                }
            }
            var along = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.right;
            var across = new Vector2(-along.y, along.x);
            return new Footprint((a + b) * 0.5f, along, across, new Vector2((b - a).magnitude * 0.5f, wallThickness * 0.5f));
        }

        /// <summary>A raised surface as a rectangle aligned with the plane's own heading.</summary>
        static Footprint SurfaceFootprint(Vector3[] world, Quaternion rotation)
        {
            var right = rotation * Vector3.right;
            var axisX = new Vector2(right.x, right.z);
            if (axisX.sqrMagnitude < 0.01f)
            {
                var fwd = rotation * Vector3.forward;
                axisX = new Vector2(fwd.x, fwd.z);
            }
            axisX = axisX.sqrMagnitude > 1e-6f ? axisX.normalized : Vector2.right;
            var axisZ = new Vector2(-axisX.y, axisX.x);

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in world)
            {
                var f = Flat(p);
                float x = Vector2.Dot(f, axisX), z = Vector2.Dot(f, axisZ);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            var centre = axisX * ((minX + maxX) * 0.5f) + axisZ * ((minZ + maxZ) * 0.5f);
            return new Footprint(centre, axisX, axisZ, new Vector2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f));
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        /// <summary>True if a detected wall lies between two points in plan view (i.e. the spot is in another room).</summary>
        public bool WallBetween(Vector3 from, Vector3 to)
        {
            Vector2 p = Flat(from), q = Flat(to);
            foreach (var wall in m_Walls.Values)
            {
                var a = wall.centre - wall.axisX * wall.half.x;
                var b = wall.centre + wall.axisX * wall.half.x;
                if (SegmentsCross(p, q, a, b))
                    return true;
            }
            return false;
        }

        static bool SegmentsCross(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(b - a, q - a);
            float d3 = Cross(q - p, a - p), d4 = Cross(q - p, b - p);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        /// <summary>For Editor testing: adds a real-room obstacle by hand.</summary>
        public void AddTestObstacle(Footprint footprint, bool isWall)
        {
            (isWall ? m_Walls : m_Furniture)[Guid.NewGuid()] = footprint;
            Changed?.Invoke();
        }
    }
}
