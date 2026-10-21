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
    /// <summary>What a detected surface is, for obstacle tests and the room preview.</summary>
    public enum SurfaceKind
    {
        Floor,
        Wall,
        Ceiling,
        /// <summary>A raised horizontal surface: bed, table, sofa seat, desk…</summary>
        Furniture,
    }

    /// <summary>One detected real-room surface as a world-space rectangle.</summary>
    public struct RoomSurface
    {
        public SurfaceKind kind;
        public Vector3 centre;
        public Quaternion rotation; // local X/Y span the rectangle, local Z is its normal
        public Vector2 size;
        /// <summary>A wall that isn't solid (door, window, curtain…): nothing walks through it, but nothing hangs on it.</summary>
        public bool opening;
        /// <summary>What the runtime recognised it as ("Bed", "Sofa", "Table"…), or "Unknown".</summary>
        public string label;
    }

    /// <summary>
    /// The real room as obstacles, from PICO plane detection (in the emulator, its simulated bedroom).
    /// Walls, doors and windows become thin barriers nothing on the floor may cross; raised horizontal
    /// surfaces (bed, table, sofa, desk…) become footprints that floor-standing furniture must avoid.
    /// Surfaces are read from each plane's outline (or its 2D extent when there is no outline), which
    /// also tells which way the plane faces.
    /// </summary>
    public class SceneObstacles : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [SerializeField] FloorDetector floor;
        [Tooltip("Horizontal surfaces at least this far above the floor count as real furniture (low enough to catch platform beds).")]
        [SerializeField] float minObstacleHeight = 0.04f;
        [Tooltip("Horizontal surfaces higher than this above the floor are treated as ceiling, not furniture.")]
        [SerializeField] float maxObstacleHeight = 1.6f;
        [Tooltip("Thickness given to walls when testing overlap (m).")]
        [SerializeField] float wallThickness = 0.06f;

        readonly Dictionary<Guid, RoomSurface> m_Surfaces = new Dictionary<Guid, RoomSurface>();
        readonly Dictionary<Guid, Footprint> m_Walls = new Dictionary<Guid, Footprint>();
        readonly Dictionary<Guid, Footprint> m_Furniture = new Dictionary<Guid, Footprint>();

        public IEnumerable<Footprint> Walls => m_Walls.Values;
        public IEnumerable<Footprint> Furniture => m_Furniture.Values;
        public IEnumerable<RoomSurface> Surfaces => m_Surfaces.Values;

        /// <summary>Solid walls (not doors, windows or curtains): where furniture backs up and art hangs.</summary>
        public IEnumerable<RoomSurface> SolidWalls
        {
            get
            {
                foreach (var s in m_Surfaces.Values)
                    if (s.kind == SurfaceKind.Wall && !s.opening)
                        yield return s;
            }
        }

        /// <summary>Raised horizontal surfaces (bed, sofa, table tops…) with their height above the floor.</summary>
        public IEnumerable<(RoomSurface surface, float height)> FurnitureTops
        {
            get
            {
                float floorY = floor != null ? floor.FloorY : 0f;
                foreach (var s in m_Surfaces.Values)
                    if (s.kind == SurfaceKind.Furniture && Mathf.Abs((s.rotation * Vector3.forward).y) >= 0.5f)
                        yield return (s, s.centre.y - floorY);
            }
        }

        /// <summary>Doors, windows and curtains.</summary>
        public IEnumerable<RoomSurface> Openings
        {
            get
            {
                foreach (var s in m_Surfaces.Values)
                    if (s.kind == SurfaceKind.Wall && s.opening)
                        yield return s;
            }
        }
        public int WallCount => m_Walls.Count;
        public int FurnitureCount => m_Furniture.Count;

        /// <summary>Raised when the set of real-room surfaces changes.</summary>
        public event Action Changed;

        string m_LastSummary;
        readonly HashSet<Guid> m_UprightObjects = new HashSet<Guid>();
        const float MaxMergedSide = 3.2f;

        void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();
            if (floor == null)
                floor = GetComponent<FloorDetector>();
        }

        void OnEnable()
        {
            if (floor != null)
                floor.FloorChanged += OnFloorChanged;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            PXR_Manager.PlaneDetectionDataUpdated += OnPlanesUpdated;
#endif
        }

        void OnDisable()
        {
            if (floor != null)
                floor.FloorChanged -= OnFloorChanged;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            PXR_Manager.PlaneDetectionDataUpdated -= OnPlanesUpdated;
#endif
        }

        // Furniture vs floor vs ceiling depends on height above the floor.
        // Vertical shift applied to every stored surface so the scan lines up with the tracking floor.
        float m_AppliedOffset;

        void OnFloorChanged(FloorDetector _)
        {
            float delta = floor.ScanOffsetY - m_AppliedOffset;
            if (Mathf.Abs(delta) > 1e-4f)
            {
                var keys = new List<Guid>(m_Surfaces.Keys);
                foreach (var key in keys)
                {
                    var s = m_Surfaces[key];
                    s.centre.y += delta;
                    m_Surfaces[key] = s;
                }
                m_AppliedOffset = floor.ScanOffsetY;
            }
            if (m_Surfaces.Count == 0)
                return;
            Rebuild();
            Changed?.Invoke();
        }

#if ENABLE_PICO_XR_SDK
        void OnPlanesUpdated(List<PxrPlaneData> planes)
        {
            bool changed = false;
            foreach (var plane in planes)
            {
                if (plane.state == MeshChangeState.Unchanged)
                    continue;
                changed |= m_Surfaces.Remove(plane.uuid);
                if (plane.state == MeshChangeState.Removed)
                    continue;
                if (!TryMeasure(plane, out var surface))
                    continue;
                if (!TryClassify(plane, surface, out surface.kind))
                    continue;
                surface.opening = surface.kind == SurfaceKind.Wall && plane.label != PxrSemanticLabel.Wall && plane.label != PxrSemanticLabel.Unknown;
                surface.label = plane.label.ToString();
                m_Surfaces[plane.uuid] = surface;
                if (plane.state == MeshChangeState.Added)
                    Debug.Log($"[PaletteRoom] Plane {plane.label} {plane.orientationMode} → {surface.kind}, {surface.size.x:F2}×{surface.size.y:F2} m at y={surface.centre.y:F2}");
                changed = true;
            }
            if (!changed)
                return;
            Rebuild();
            var summary = $"{m_Surfaces.Count} surfaces → {m_Walls.Count} walls, {m_Furniture.Count} furniture obstacles";
            if (summary != m_LastSummary)
                Debug.Log($"[PaletteRoom] Room scan: {summary}.");
            m_LastSummary = summary;
            Changed?.Invoke();
        }

        /// <summary>
        /// Converts a plane to a world rectangle (local Z = normal). Which local axis is the plane's normal
        /// differs between conventions, so it is read from the outline: the plane's vertices are flat
        /// along exactly one local axis. Without an outline, the 2D extent spans local X/Y.
        /// </summary>
        bool TryMeasure(PxrPlaneData plane, out RoomSurface surface)
        {
            surface = default;
            Vector3 localCentre, spread;
            if (plane.vertices != null && plane.vertices.Length >= 3)
            {
                var bounds = new Bounds(plane.vertices[0], Vector3.zero);
                foreach (var v in plane.vertices)
                    bounds.Encapsulate(v);
                localCentre = bounds.center;
                spread = bounds.size;
            }
            else
            {
                var extent = new Vector2(plane.box2D.extent.width, plane.box2D.extent.height);
                // OpenXR rectangles give the offset of their lower-left corner.
                localCentre = new Vector3(plane.box2D.offset.x + extent.x / 2f, plane.box2D.offset.y + extent.y / 2f, 0f);
                spread = new Vector3(extent.x, extent.y, 0f);
            }

            int n = spread.x <= spread.y && spread.x <= spread.z ? 0 : spread.y <= spread.z ? 1 : 2;
            int a = (n + 1) % 3, b = (n + 2) % 3;
            var size = new Vector2(spread[a], spread[b]);
            if (size.x <= 0.05f || size.y <= 0.05f)
                return false;

            var rotation = TrackingRotationToWorld(plane.rotation);
            var normal = rotation * Axis(n);
            var up = rotation * Axis(b);
            surface.rotation = Quaternion.LookRotation(normal, up); // local X ≈ axis a, Y = axis b, Z = normal
            surface.size = size;
            surface.centre = TrackingToWorld(plane.position) + rotation * localCentre + Vector3.up * m_AppliedOffset;
            return true;
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        /// <summary>
        /// Floor, wall, ceiling or a raised surface (refined by height in <see cref="Rebuild"/>). Small
        /// upright pieces (pictures, cabinet fronts) are not walls and are skipped.
        /// </summary>
        static bool TryClassify(PxrPlaneData plane, RoomSurface s, out SurfaceKind kind)
        {
            kind = SurfaceKind.Furniture;
            var normal = s.rotation * Vector3.forward;
            bool upright = Mathf.Abs(normal.y) < 0.5f;
            switch (plane.label)
            {
                case PxrSemanticLabel.Floor: kind = SurfaceKind.Floor; return true;
                case PxrSemanticLabel.Ceiling: kind = SurfaceKind.Ceiling; return true;
                case PxrSemanticLabel.Wall:
                case PxrSemanticLabel.Door:
                case PxrSemanticLabel.Window:
                case PxrSemanticLabel.Opening:
                    kind = SurfaceKind.Wall;
                    return true;
            }
            if (upright && IsObject(plane.label))
            {
                // A face of a real object (plant, lamp, cabinet, fridge…): it takes up floor space.
                kind = SurfaceKind.Furniture;
                return true;
            }
            if (upright)
            {
                // Unlabelled upright surfaces only count as walls when they're wall-sized.
                kind = SurfaceKind.Wall;
                float tall = Mathf.Abs((s.rotation * Vector3.up).y) > 0.7f ? s.size.y : s.size.x;
                float wide = Mathf.Max(s.size.x, s.size.y);
                return tall >= 1.5f && wide >= 1f;
            }
            // The outline gives the normal's axis but not which way it points, so trust the runtime for that.
            if (plane.orientationMode == PxrPlaneOrientation.HorizontalDownward)
                kind = SurfaceKind.Ceiling;
            return true;
        }

        static bool IsObject(PxrSemanticLabel label)
        {
            switch (label)
            {
                case PxrSemanticLabel.Table:
                case PxrSemanticLabel.Sofa:
                case PxrSemanticLabel.Chair:
                case PxrSemanticLabel.Cabinet:
                case PxrSemanticLabel.Bed:
                case PxrSemanticLabel.Plant:
                case PxrSemanticLabel.Screen:
                case PxrSemanticLabel.Refrigerator:
                case PxrSemanticLabel.WashingMachine:
                case PxrSemanticLabel.AirConditioner:
                case PxrSemanticLabel.Lamp:
                    return true;
                default:
                    return false;
            }
        }

        Vector3 TrackingToWorld(Vector3 p)
        {
            var space = TrackingSpace;
            return space != null ? space.TransformPoint(p) : p;
        }

        Quaternion TrackingRotationToWorld(Quaternion r)
        {
            var space = TrackingSpace;
            return space != null ? space.rotation * r : r;
        }

        Transform TrackingSpace => xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null
            ? xrOrigin.CameraFloorOffsetObject.transform
            : null;
#endif

        /// <summary>Re-derives walls and furniture footprints from the surfaces, using the current floor height.</summary>
        void Rebuild()
        {
            m_Walls.Clear();
            m_Furniture.Clear();
            m_UprightObjects.Clear();
            float floorY = floor != null ? floor.FloorY : 0f;
            var keys = new List<Guid>(m_Surfaces.Keys);
            foreach (var key in keys)
            {
                var s = m_Surfaces[key];
                if (s.kind == SurfaceKind.Wall)
                {
                    m_Walls[key] = WallFootprint(s);
                    continue;
                }
                if (s.kind != SurfaceKind.Furniture)
                    continue;
                if (Mathf.Abs((s.rotation * Vector3.forward).y) < 0.5f)
                {
                    // An upright face of a real object: its width along the floor, and a depth guessed
                    // from it (a plant is about as deep as it is wide; a cabinet front less so).
                    m_Furniture[key] = ObjectFootprint(s);
                    m_UprightObjects.Add(key);
                    continue;
                }
                float height = s.centre.y - floorY;
                if (height < minObstacleHeight)
                    s.kind = SurfaceKind.Floor;
                else if (height > maxObstacleHeight)
                    s.kind = SurfaceKind.Ceiling;
                else
                    m_Furniture[key] = SurfaceFootprint(s);
                m_Surfaces[key] = s;
            }
            MergeTouchingFurniture();
        }

        /// <summary>
        /// Real furniture is often scanned as several surfaces at different heights — a mattress on a
        /// platform bed, a desk with a shelf. Surfaces that overlap or touch become one obstacle: the
        /// rectangle around both, aligned with the larger one.
        /// </summary>
        void MergeTouchingFurniture()
        {
            const float touch = 0.05f;
            bool merged = true;
            while (merged)
            {
                merged = false;
                var keys = new List<Guid>(m_Furniture.Keys);
                for (int i = 0; i < keys.Count && !merged; i++)
                {
                    for (int j = i + 1; j < keys.Count && !merged; j++)
                    {
                        var a = m_Furniture[keys[i]];
                        var b = m_Furniture[keys[j]];
                        if (m_UprightObjects.Contains(keys[i]) || m_UprightObjects.Contains(keys[j]))
                            continue; // plants, lamps and cabinets stay separate obstacles
                        if (!Grown(a, touch).Overlaps(Grown(b, touch)))
                            continue;
                        bool aLarger = a.half.x * a.half.y >= b.half.x * b.half.y;
                        var union = aLarger ? Enclose(a, b) : Enclose(b, a);
                        if (union.half.x * 2f > MaxMergedSide || union.half.y * 2f > MaxMergedSide)
                            continue; // two pieces of furniture side by side, not one piece scanned in parts
                        m_Furniture[keys[i]] = union;
                        m_Furniture.Remove(keys[j]);
                        merged = true;
                    }
                }
            }
        }

        static Footprint Grown(Footprint f, float by) => new Footprint(f.centre, f.axisX, f.axisZ, f.half + Vector2.one * by);

        /// <summary>The smallest rectangle aligned with <paramref name="basis"/> that contains both footprints.</summary>
        static Footprint Enclose(Footprint basis, Footprint other)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var f in new[] { basis, other })
            {
                for (int i = 0; i < 4; i++)
                {
                    var corner = f.centre + f.axisX * f.half.x * ((i & 1) == 0 ? -1f : 1f) + f.axisZ * f.half.y * ((i & 2) == 0 ? -1f : 1f);
                    var local = corner - basis.centre;
                    var uv = new Vector2(Vector2.Dot(local, basis.axisX), Vector2.Dot(local, basis.axisZ));
                    min = Vector2.Min(min, uv);
                    max = Vector2.Max(max, uv);
                }
            }
            var mid = (min + max) * 0.5f;
            return new Footprint(basis.centre + basis.axisX * mid.x + basis.axisZ * mid.y, basis.axisX, basis.axisZ, (max - min) * 0.5f);
        }

        /// <summary>A wall as a thin strip along its horizontal extent.</summary>
        Footprint WallFootprint(RoomSurface s)
        {
            // The rectangle's local X or Y, whichever is more horizontal, runs along the wall.
            var x = s.rotation * Vector3.right;
            var y = s.rotation * Vector3.up;
            bool xHorizontal = Mathf.Abs(x.y) <= Mathf.Abs(y.y);
            var along3 = xHorizontal ? x : y;
            float length = xHorizontal ? s.size.x : s.size.y;
            var along = new Vector2(along3.x, along3.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            var across = new Vector2(-along.y, along.x);
            return new Footprint(Flat(s.centre), along, across, new Vector2(length * 0.5f, wallThickness * 0.5f));
        }

        /// <summary>A real object seen as an upright face: its floor outline, centred on the face.</summary>
        static Footprint ObjectFootprint(RoomSurface s)
        {
            var x = s.rotation * Vector3.right;
            var y = s.rotation * Vector3.up;
            bool xHorizontal = Mathf.Abs(x.y) <= Mathf.Abs(y.y);
            var along3 = Vector3.ProjectOnPlane(xHorizontal ? x : y, Vector3.up);
            float width = xHorizontal ? s.size.x : s.size.y;
            var along = new Vector2(along3.x, along3.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            var across = new Vector2(-along.y, along.x);
            float depth = Mathf.Clamp(width * 0.8f, 0.3f, 0.8f);
            return new Footprint(Flat(s.centre), along, across, new Vector2(width * 0.5f, depth * 0.5f));
        }

        /// <summary>A raised horizontal surface as its rectangle seen from above.</summary>
        static Footprint SurfaceFootprint(RoomSurface s)
        {
            var x = s.rotation * Vector3.right;
            var y = s.rotation * Vector3.up;
            var axisX = new Vector2(x.x, x.z);
            var axisZ = new Vector2(y.x, y.z);
            axisX = axisX.sqrMagnitude > 1e-6f ? axisX.normalized : Vector2.right;
            axisZ = axisZ.sqrMagnitude > 1e-6f ? axisZ.normalized : new Vector2(-axisX.y, axisX.x);
            return new Footprint(Flat(s.centre), axisX, axisZ, s.size * 0.5f);
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

        /// <summary>For Editor testing: adds a real-room surface by hand.</summary>
        public void AddTestSurface(RoomSurface surface)
        {
            m_Surfaces[Guid.NewGuid()] = surface;
            Rebuild();
            Changed?.Invoke();
        }
    }
}
