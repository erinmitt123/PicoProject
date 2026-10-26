using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The real room as the headset's 3D scan (the PICO spatial mesh) sees it. Plane detection only gives
    /// rough boxes, often much bigger than the object (a 2.7 × 2.8 m "bed"), and misses things altogether;
    /// the mesh has the actual shape. It is turned into a plan-view grid holding, per 5 cm cell, the
    /// lowest real geometry above the floor, so a piece is blocked exactly where something real stands
    /// in the height it occupies — and is free to slide under a real table top it clears.
    /// </summary>
    public partial class SceneObstacles
    {
        const float MeshCell = 0.05f;
        /// <summary>Geometry lower than this above the floor is the floor itself (scan noise, rugs, thresholds).</summary>
        const float MeshFloorClearance = 0.07f;
        /// <summary>Geometry higher than this is ceiling, lights and wall tops, not floor obstacles.</summary>
        const float MeshMaxHeight = 2.2f;
        /// <summary>Pieces may touch real things: cells this close to a piece's edge don't count.</summary>
        const float MeshEdgeTolerance = 0.03f;
        /// <summary>A lone sample is scan noise; something real covers a few cells.</summary>
        const int MeshMinCells = 3;
        const float MeshSampleSpacing = 0.035f;

        readonly Dictionary<long, float> m_MeshLow = new Dictionary<long, float>();
        readonly List<Vector3> m_Vertices = new List<Vector3>();
        readonly List<int> m_Triangles = new List<int>();
        int m_MeshSignature;
        float m_MeshFloorY = float.NaN;
        float m_NextMeshCheck;

        /// <summary>True once the 3D scan has produced anything standing on the floor.</summary>
        public bool HasMesh => m_MeshLow.Count > 0;

        /// <summary>Raised when the 3D scan's obstacles change.</summary>
        public event Action MeshChanged;

        void Update()
        {
            if (scanAlignedRoot == null || Time.unscaledTime < m_NextMeshCheck)
                return;
            m_NextMeshCheck = Time.unscaledTime + 1f;
            float floorY = floor != null ? floor.FloorY : 0f;
            int signature = MeshSignature();
            if (signature == m_MeshSignature && Mathf.Abs(floorY - m_MeshFloorY) < 0.005f)
                return;
            m_MeshSignature = signature;
            m_MeshFloorY = floorY;
            RebuildMeshGrid(floorY);
        }

        int MeshSignature()
        {
            unchecked
            {
                int hash = 17;
                foreach (var filter in scanAlignedRoot.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;
                    hash = hash * 31 + mesh.GetInstanceID();
                    hash = hash * 31 + mesh.vertexCount;
                    hash = hash * 31 + filter.transform.position.GetHashCode();
                }
                return hash;
            }
        }

        void RebuildMeshGrid(float floorY)
        {
            m_MeshLow.Clear();
            int triangles = 0;
            foreach (var filter in scanAlignedRoot.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0)
                    continue;
                var toWorld = filter.transform.localToWorldMatrix;
                mesh.GetVertices(m_Vertices);
                for (int i = 0; i < m_Vertices.Count; i++)
                    m_Vertices[i] = toWorld.MultiplyPoint3x4(m_Vertices[i]);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    mesh.GetTriangles(m_Triangles, sub);
                    for (int t = 0; t + 2 < m_Triangles.Count; t += 3)
                    {
                        AddTriangle(m_Vertices[m_Triangles[t]], m_Vertices[m_Triangles[t + 1]], m_Vertices[m_Triangles[t + 2]], floorY);
                        triangles++;
                    }
                }
            }
            if (triangles > 0)
                Debug.Log($"[PaletteRoom] Room mesh: {triangles} triangles → {m_MeshLow.Count} occupied floor cells.");
            MeshChanged?.Invoke();
        }

        /// <summary>Samples a triangle and records, per cell, the lowest point that stands above the floor.</summary>
        void AddTriangle(Vector3 a, Vector3 b, Vector3 c, float floorY)
        {
            float ha = a.y - floorY, hb = b.y - floorY, hc = c.y - floorY;
            if (ha < MeshFloorClearance && hb < MeshFloorClearance && hc < MeshFloorClearance)
                return; // floor
            if (ha > MeshMaxHeight && hb > MeshMaxHeight && hc > MeshMaxHeight)
                return; // ceiling
            float longest = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
            int n = Mathf.Clamp(Mathf.CeilToInt(longest / MeshSampleSpacing), 1, 200);
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n - i; j++)
                {
                    float u = i / (float)n, v = j / (float)n;
                    var p = a + (b - a) * u + (c - a) * v;
                    float h = p.y - floorY;
                    if (h < MeshFloorClearance || h > MeshMaxHeight)
                        continue;
                    long key = CellKey(p.x, p.z);
                    if (!m_MeshLow.TryGetValue(key, out float low) || h < low)
                        m_MeshLow[key] = h;
                }
            }
        }

        static long CellKey(float x, float z) => CellKey(Mathf.FloorToInt(x / MeshCell), Mathf.FloorToInt(z / MeshCell));
        static long CellKey(int ix, int iz) => ((long)ix << 32) ^ (uint)iz;

        /// <summary>
        /// True if the 3D scan has something real inside <paramref name="footprint"/> lower than
        /// <paramref name="top"/> metres above the floor; <paramref name="where"/> says where and how high.
        /// </summary>
        public bool MeshBlocks(Footprint footprint, float top, out string where)
        {
            where = null;
            if (m_MeshLow.Count == 0)
                return false;
            var f = footprint;
            float ex = Mathf.Abs(f.axisX.x) * f.half.x + Mathf.Abs(f.axisZ.x) * f.half.y;
            float ez = Mathf.Abs(f.axisX.y) * f.half.x + Mathf.Abs(f.axisZ.y) * f.half.y;
            int x0 = Mathf.FloorToInt((f.centre.x - ex) / MeshCell), x1 = Mathf.FloorToInt((f.centre.x + ex) / MeshCell);
            int z0 = Mathf.FloorToInt((f.centre.y - ez) / MeshCell), z1 = Mathf.FloorToInt((f.centre.y + ez) / MeshCell);
            float hx = f.half.x - MeshEdgeTolerance, hz = f.half.y - MeshEdgeTolerance;
            if (hx <= 0f || hz <= 0f)
                return false;

            int hits = 0;
            var sum = Vector2.zero;
            float lowest = float.MaxValue;
            for (int ix = x0; ix <= x1; ix++)
            {
                for (int iz = z0; iz <= z1; iz++)
                {
                    if (!m_MeshLow.TryGetValue(CellKey(ix, iz), out float low) || low >= top)
                        continue;
                    var cell = new Vector2((ix + 0.5f) * MeshCell, (iz + 0.5f) * MeshCell);
                    var local = cell - f.centre;
                    if (Mathf.Abs(Vector2.Dot(local, f.axisX)) > hx || Mathf.Abs(Vector2.Dot(local, f.axisZ)) > hz)
                        continue;
                    if (OnDetectedWall(cell))
                        continue; // the wall itself, already a barrier; its scan is rarely flush with the plane
                    hits++;
                    sum += cell;
                    lowest = Mathf.Min(lowest, low);
                }
            }
            if (hits < MeshMinCells)
                return false;
            var at = sum / hits;
            where = $"something real in the room ({hits * MeshCell * MeshCell * 10000f:F0} cm² of it, from {lowest:F2} m up, at {at.x:F1}, {at.y:F1})";
            return true;
        }

        /// <summary>
        /// True if a scan cell is part of a detected wall rather than something standing in front of it:
        /// the mesh of a wall sits a few centimetres either side of its plane, and counting it would stop
        /// anything backing onto the wall, pushing the sample room out into (or past) the walls.
        /// </summary>
        bool OnDetectedWall(Vector2 cell)
        {
            const float reach = 0.15f;
            foreach (var w in m_Walls.Values)
            {
                var local = cell - w.centre;
                if (Mathf.Abs(Vector2.Dot(local, w.axisX)) <= w.half.x + reach &&
                    Mathf.Abs(Vector2.Dot(local, w.axisZ)) <= w.half.y + reach)
                    return true;
            }
            return false;
        }

        /// <summary>For Editor testing: replaces the 3D scan with the given meshes' geometry.</summary>
        public void RebuildMeshFrom(Transform root)
        {
            var previous = scanAlignedRoot;
            scanAlignedRoot = root;
            float floorY = floor != null ? floor.FloorY : 0f;
            m_MeshSignature = MeshSignature();
            m_MeshFloorY = floorY;
            RebuildMeshGrid(floorY);
            scanAlignedRoot = previous;
            m_NextMeshCheck = float.MaxValue; // keep the test geometry
        }
    }
}
