using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Owns every piece of furniture in the room: the pre-arranged sample room and items added from the
    /// shopping list. All pieces live under one root that sits at floor height, so a floor change moves
    /// the whole room. Room-space coordinates are metres with +Z pointing away from the user.
    /// </summary>
    public partial class RoomFurniture : MonoBehaviour
    {
        [Tooltip("Models and materials for the catalog (Palette Room ▸ Rebuild Furniture Assets).")]
        [SerializeField] FurnitureLibrary library;
        [Tooltip("Plain URP Lit material, used if the library is missing anything.")]
        [SerializeField] Material baseMaterial;
        [Tooltip("Real-room walls and furniture from plane detection; pieces are placed around them.")]
        [SerializeField] SceneObstacles sceneObstacles;
        [Tooltip("Distance from the user to the sample room's centre line.")]
        [SerializeField] float distanceFromUser = 1.9f;
        [Tooltip("Distance in front of the user where shopping-list items appear.")]
        [SerializeField] float addDistance = 1.3f;
        [SerializeField] float dropHeight = 0.35f;
        [SerializeField] float dropDuration = 0.45f;
        [SerializeField] float dropStagger = 0.06f;

        readonly List<FurnitureItem> m_Items = new List<FurnitureItem>();
        FurnitureFactory m_Factory;
        Transform m_Root;
        Transform m_Head; // the user, who is inside the room: pieces can't go across a wall from them

        public IReadOnlyList<FurnitureItem> Items => m_Items;
        public FurnitureLibrary Library => library;

        /// <summary>Raised whenever pieces are added or removed.</summary>
        public event Action Changed;

        /// <summary>The plain URP Lit material the room is built from.</summary>
        public Material BaseMaterial => baseMaterial;

        FurnitureFactory Factory => m_Factory ?? (m_Factory = new FurnitureFactory(library, baseMaterial));

        // The sample room as first laid out, before the user has touched anything: if the 3D room scan
        // arrives (or changes) afterwards and shows a piece standing in something real, it is laid out again.
        bool m_Pristine;
        Transform m_LayoutHead;
        int m_Relayouts;
        const int MaxRelayouts = 2;

        /// <summary>Real geometry this high above the floor or lower (table tops, bed frames) keeps floor pieces out from under it.</summary>
        const float UnderRealFurniture = 1.2f;

        void Awake()
        {
            if (sceneObstacles == null)
                sceneObstacles = FindFirstObjectByType<SceneObstacles>();
        }

        void OnEnable()
        {
            if (sceneObstacles != null)
                sceneObstacles.MeshChanged += OnRoomScanChanged;
        }

        void OnDisable()
        {
            if (sceneObstacles != null)
                sceneObstacles.MeshChanged -= OnRoomScanChanged;
        }

        /// <summary>The user has moved or added something: their arrangement is no longer re-laid automatically.</summary>
        public void MarkEdited() => m_Pristine = false;

        /// <summary>
        /// Resizes <paramref name="item"/> within its real-world range. A floor piece that would grow into
        /// another piece, a wall or real furniture keeps its size; returns false then (or at the range's end).
        /// </summary>
        public bool TryResize(FurnitureItem item, float size, out string blocker)
        {
            blocker = null;
            float before = item.Size;
            if (Mathf.Approximately(item.SetSize(size), before))
                return false;
            blocker = WhatBlocks(item, item.transform.position, item.transform.rotation);
            if (blocker != null)
            {
                item.SetSize(before);
                return false;
            }
            MarkEdited();
            Changed?.Invoke();
            return true;
        }

        void OnRoomScanChanged()
        {
            if (!m_Pristine || m_LayoutHead == null || m_Root == null || m_Relayouts >= MaxRelayouts)
                return;
            foreach (var item in m_Items)
            {
                if (item == null || !item.BlocksFloor)
                    continue;
                var blocker = WhatBlocks(item, item.transform.position, item.transform.rotation);
                if (blocker == null)
                    continue;
                Debug.Log($"[PaletteRoom] The room scan shows the {item.DisplayName.ToLowerInvariant()} standing in {blocker}; laying the room out again.");
                // The scan grows every second on device. Re-laying on every update kept tearing the room
                // down and dropping it in again (flicker), so it is re-laid a couple of times at most, in place.
                m_Relayouts++;
                BuildSampleRoom(m_LayoutHead, m_Root.position.y, dropIn: false);
                return;
            }
        }

        bool BehindWall(Vector3 user, Vector3 point) => sceneObstacles != null && sceneObstacles.WallBetween(user, point);

        /// <summary>How many pieces of <paramref name="kind"/> are placed; a <paramref name="variantId"/> narrows it to one size.</summary>
        public int Count(FurnitureKind kind, string variantId = null)
        {
            Prune();
            int n = 0;
            foreach (var item in m_Items)
            {
                if (item.Kind == kind && (variantId == null || item.VariantId == variantId))
                    n++;
            }
            return n;
        }

        /// <summary>Replaces everything with the sample living room, laid out to fit the real room (see RoomFurniture.Layout.cs).</summary>
        public void SpawnSampleRoom(Transform head, float floorY)
        {
            m_Relayouts = 0;
            BuildSampleRoom(head, floorY, dropIn: true);
        }

        void BuildSampleRoom(Transform head, float floorY, bool dropIn)
        {
            Clear();
            var root = EnsureRoot(head, floorY);
            var added = LayOutSampleRoom(root, head, floorY);
            if (dropIn)
                DropIn(added);
            m_Pristine = true;
            m_LayoutHead = head;
            Debug.Log($"[PaletteRoom] Sample room placed at floor y={floorY:F3} (head y={head.position.y:F2}).");
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds one piece in front of the user, facing them. Floor-standing pieces go to the nearest
        /// spot where they don't intersect anything already in the room.
        /// </summary>
        public FurnitureItem Add(FurnitureKind kind, string variantId, Transform head, float floorY, bool dropIn = true)
        {
            m_Pristine = false;
            var root = EnsureRoot(head, floorY);
            var forward = FlatForward(head);
            var rotation = Quaternion.LookRotation(-forward, Vector3.up);
            var preferred = head.position + forward * addDistance;
            float height = kind == FurnitureKind.WallArt ? 1.2f : 0f; // hung at picture height

            var item = Factory.Create(kind, FurnitureCatalog.Get(kind).Variant(variantId), root);
            var spot = new Vector3(preferred.x, floorY + height, preferred.z);
            if (item.BlocksFloor && !TryFindFreeSpot(item, spot, rotation, head.position, out spot, maxRadius: 4.5f))
            {
                if (dropIn)
                {
                    // Never forced into a spot it can't stand on.
                    Debug.LogWarning($"[PaletteRoom] No free floor space for {item.DisplayName} nearby; not added.");
                    DestroyObject(item.gameObject);
                    return null;
                }
                // Going straight into the user's hand: the drop is checked when they let go.
                Debug.Log($"[PaletteRoom] No free floor space for {item.DisplayName} nearby; it goes into the hand to be set down somewhere free.");
            }
            item.transform.SetPositionAndRotation(spot, rotation);
            m_Items.Add(item);
            if (item.Mount == FurnitureMount.Wall && !TryHangOnWall(item, head, floorY))
                Debug.Log($"[PaletteRoom] No scanned wall nearby for the {item.DisplayName.ToLowerInvariant()}; it floats in front of the user.");
            if (item.Mount == FurnitureMount.Free && kind != FurnitureKind.ThrowPillow && !TryPlaceAccessory(item, head))
                Debug.Log($"[PaletteRoom] Nothing to set the {item.DisplayName.ToLowerInvariant()} on yet; it's on the floor in front of the user.");
            if (dropIn)
                DropIn(new List<FurnitureItem> { item });
            Changed?.Invoke();
            return item;
        }

        /// <summary>True if <paramref name="item"/> at this pose would not intersect another floor-standing piece.</summary>
        public bool IsFree(FurnitureItem item, Vector3 position, Quaternion rotation) => WhatBlocks(item, position, rotation) == null;

        /// <summary>
        /// What stops <paramref name="item"/> standing here — another piece, a wall, or real furniture —
        /// or null if nothing does. Used for placement, and to explain refused drops in the log.
        /// </summary>
        public string WhatBlocks(FurnitureItem item, Vector3 position, Quaternion rotation)
        {
            if (item.Mount != FurnitureMount.Floor)
                return null;
            var footprints = item.FootprintsAt(position, rotation);
            // Wall strips only stop a piece that overlaps them: one taken right past a wall or window
            // overlaps nothing, so any part of it across a wall from the user puts it outside the room.
            if (OutsideRoom(footprints))
                return "outside the room (past a wall, door or window)";
            if (!item.BlocksFloor)
                return null;
            foreach (var other in m_Items)
            {
                if (other == null || other == item || !other.BlocksFloor)
                    continue;
                if (FurnitureItem.Overlap(footprints, other.CurrentFootprints))
                    return $"the {other.DisplayName.ToLowerInvariant()}";
            }
            // The real room: nothing goes through a wall, and virtual pieces stand around real furniture.
            if (sceneObstacles != null)
            {
                foreach (var wall in sceneObstacles.Walls)
                    if (FurnitureItem.Overlap(footprints, new[] { wall }))
                        return $"a wall ({wall.half.x * 2f:F1} m, centred at {wall.centre.x:F1}, {wall.centre.y:F1})";
                return WhatRealBlocks(footprints, TopAbove(item, position));
            }
            return null;
        }

        bool OutsideRoom(Footprint[] footprints)
        {
            if (sceneObstacles == null || m_Head == null)
                return false;
            var user = m_Head.position;
            foreach (var f in footprints)
            {
                for (int i = 0; i < 4; i++)
                {
                    var corner = f.centre + f.axisX * f.half.x * ((i & 1) == 0 ? -1f : 1f) + f.axisZ * f.half.y * ((i & 2) == 0 ? -1f : 1f);
                    if (sceneObstacles.WallBetween(user, new Vector3(corner.x, user.y, corner.y)))
                        return true;
                }
            }
            return false;
        }

        /// <summary>How high the piece reaches above the floor when standing at <paramref name="position"/>.</summary>
        float TopAbove(FurnitureItem item, Vector3 position)
        {
            float floorY = m_Root != null ? m_Root.position.y : position.y;
            return LocalBox(item).max.y + Mathf.Max(0f, position.y - floorY);
        }

        /// <summary>
        /// Real things in the way up to <paramref name="top"/> metres: the 3D room scan when there is one
        /// (exact shapes), otherwise the rough boxes from plane detection.
        /// </summary>
        string WhatRealBlocks(Footprint[] footprints, float top)
        {
            if (sceneObstacles == null)
                return null;
            if (sceneObstacles.HasMesh)
            {
                // Nothing goes under a real table, desk or bed either, even when it would clear the top:
                // a coffee table tucked under a dining table reads as one stacked on (or in) the other.
                top = Mathf.Max(top, UnderRealFurniture);
                foreach (var f in footprints)
                    if (sceneObstacles.MeshBlocks(f, top, out var where))
                        return where;
                return null;
            }
            foreach (var real in sceneObstacles.Furniture)
                if (FurnitureItem.Overlap(footprints, new[] { real }))
                    return $"real furniture ({real.half.x * 2f:F1}×{real.half.y * 2f:F1} m at {real.centre.x:F1}, {real.centre.y:F1})";
            return null;
        }

        /// <summary>
        /// Searches outward from <paramref name="preferred"/> in rings for the closest free spot that
        /// is neither on top of the user nor out of reach.
        /// </summary>
        public bool TryFindFreeSpot(FurnitureItem item, Vector3 preferred, Quaternion rotation, Vector3 userPosition, out Vector3 spot,
            float maxRadius = 3f)
        {
            const float step = 0.15f, minFromUser = 0.6f;
            float maxFromUser = Mathf.Max(4f, maxRadius + 1.5f);
            var user = new Vector2(userPosition.x, userPosition.z);
            for (float r = 0f; r <= maxRadius; r += step)
            {
                int samples = r <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / step));
                for (int i = 0; i < samples; i++)
                {
                    float a = i * Mathf.PI * 2f / samples;
                    var candidate = preferred + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    float fromUser = Vector2.Distance(user, new Vector2(candidate.x, candidate.z));
                    if (fromUser < minFromUser || fromUser > maxFromUser)
                        continue;
                    if (IsFree(item, candidate, rotation) && !BehindWall(userPosition, candidate))
                    {
                        spot = candidate;
                        return true;
                    }
                }
            }
            spot = preferred;
            return false;
        }

        /// <summary>Removes the most recently placed piece of this kind and size.</summary>
        public bool RemoveLast(FurnitureKind kind, string variantId)
        {
            Prune();
            for (int i = m_Items.Count - 1; i >= 0; i--)
            {
                if (m_Items[i].Kind != kind || m_Items[i].VariantId != variantId)
                    continue;
                DestroyObject(m_Items[i].gameObject);
                m_Items.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void SetFloor(float floorY)
        {
            if (m_Root == null)
                return;
            var p = m_Root.position;
            m_Root.position = new Vector3(p.x, floorY, p.z);
            Debug.Log($"[PaletteRoom] Floor moved to y={floorY:F3}; furniture follows.");
        }

        /// <summary>True if the piece stands where it is without overlapping anything (for brief scoring).</summary>
        public bool IsWellPlaced(FurnitureItem item) =>
            item == null || IsFree(item, item.transform.position, item.transform.rotation);

        /// <summary>
        /// Lays a saved lookbook design out again: its centre about two metres in front of the user,
        /// facing them as it faced the viewer when it was saved, with each part's color picks restored.
        /// A design saved in another room may not fit this one, so every floor piece must stand clear of
        /// walls, real furniture and the other pieces (nudged at most <see cref="RestoreNudge"/>). If any
        /// piece can't, nothing is laid out, the room stays as it was, and <paramref name="problem"/> says why.
        /// </summary>
        public bool Restore(Lookbook.Entry entry, Transform head, float floorY, out string problem)
        {
            problem = null;
            var root = EnsureRoot(head, floorY);
            var forward = FlatForward(head);
            var centre = head.position + forward * (distanceFromUser + 0.3f);
            centre.y = floorY;
            var frame = Quaternion.LookRotation(-forward, Vector3.up);

            // The current pieces stay put until the design is known to fit, but don't count against it.
            Prune();
            var previous = new List<FurnitureItem>(m_Items);
            m_Items.Clear();
            var added = new List<FurnitureItem>();
            foreach (var piece in entry.pieces)
            {
                if (!Enum.TryParse(piece.kind, out FurnitureKind kind) || !FurnitureCatalog.Offered(kind))
                    continue;
                var item = Factory.Create(kind, FurnitureCatalog.Get(kind).Variant(piece.variant), root);
                if (piece.size > 0f)
                    item.SetSize(piece.size);
                var position = centre + frame * piece.offset;
                position.y = floorY + piece.offset.y;
                var rotation = frame * Quaternion.Euler(piece.euler);
                item.transform.SetPositionAndRotation(position, rotation);
                m_Items.Add(item);
                added.Add(item);
                var blocker = WhatBlocks(item, position, rotation);
                if (blocker != null)
                {
                    if (!TryFindFreeSpot(item, position, rotation, head.position, out var spot, RestoreNudge))
                    {
                        problem = $"The {item.DisplayName.ToLowerInvariant()} would be in {blocker}.";
                        break;
                    }
                    item.transform.position = spot;
                }
                foreach (var pick in piece.picks)
                    foreach (var zone in item.Zones)
                        if (zone.ZoneName == pick.zone)
                            zone.SetOverride((PaletteSlot)pick.slot, 0f);
                if (piece.picks.Count > 0 && PaletteManager.Instance != null)
                    PaletteManager.Instance.RefreshItem(item);
            }

            if (problem != null)
            {
                foreach (var item in added)
                    DestroyObject(item.gameObject);
                m_Items.Clear();
                m_Items.AddRange(previous);
                Debug.Log($"[PaletteRoom] \"{entry.title}\" doesn't fit this room: {problem} Nothing was changed.");
                return false;
            }

            StopAllCoroutines();
            foreach (var item in previous)
                DestroyObject(item.gameObject);
            m_Pristine = false;
            DropIn(added);
            Debug.Log($"[PaletteRoom] Restored \"{entry.title}\" from the lookbook: {added.Count} pieces.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>How far a restored piece may shift to clear something; any further and the design doesn't fit.</summary>
        const float RestoreNudge = 0.4f;

        public void Clear()
        {
            StopAllCoroutines();
            foreach (var item in m_Items)
            {
                if (item != null)
                    DestroyObject(item.gameObject);
            }
            m_Items.Clear();
            Changed?.Invoke();
        }

        Transform EnsureRoot(Transform head, float floorY)
        {
            m_Head = head;
            if (m_Root == null)
            {
                m_Root = new GameObject("Room").transform;
                m_Root.SetParent(transform, false);
                m_Root.SetPositionAndRotation(new Vector3(head.position.x, floorY, head.position.z), Quaternion.identity);
            }
            return m_Root;
        }

        static Vector3 FlatForward(Transform head)
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
        }

        void Prune() => m_Items.RemoveAll(i => i == null);

        static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        void DropIn(List<FurnitureItem> items)
        {
            if (Application.isPlaying)
                StartCoroutine(DropRoutine(items));
        }

        // A new piece pops into being a little above its spot (swelling past full size), drops, lands
        // with a squash and a wobble, and sends a ring of the palette's accent color out across the
        // floor. Sounds: a pop as it appears and a soft thud where it lands.
        const float PopEnd = 0.22f, LandAt = 0.55f;

        IEnumerator DropRoutine(List<FurnitureItem> items)
        {
            float duration = Mathf.Max(dropDuration, 0.9f);
            var rests = new Vector3[items.Count];
            var landed = new bool[items.Count];
            var popped = new bool[items.Count];
            float volume = items.Count > 3 ? 0.55f : 1f;
            for (int i = 0; i < items.Count; i++)
            {
                rests[i] = items[i].transform.localPosition;
                items[i].transform.localPosition = rests[i] + Vector3.up * dropHeight;
                items[i].transform.localScale = Vector3.zero;
            }

            float total = duration + dropStagger * (items.Count - 1);
            for (float t = 0f; t <= total; t += Time.deltaTime)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    float k = (t - i * dropStagger) / duration;
                    if (k >= 0f && !popped[i] && items[i] != null)
                    {
                        popped[i] = true;
                        Sfx.PlayAt(SfxKind.Pop, items[i].transform.position, volume, UnityEngine.Random.Range(0.92f, 1.1f));
                    }
                    if (k >= LandAt && !landed[i] && items[i] != null)
                    {
                        landed[i] = true;
                        Sfx.PlayAt(SfxKind.Drop, items[i].transform.position, volume, UnityEngine.Random.Range(0.9f, 1.08f));
                        SpawnRing.Play(items[i]);
                    }
                    Pose(items[i], k, rests[i]);
                }
                yield return null;
            }
            for (int i = 0; i < items.Count; i++)
                Pose(items[i], 1f, rests[i]);
        }

        void Pose(FurnitureItem item, float k, Vector3 rest)
        {
            if (item == null)
                return;
            k = Mathf.Clamp01(k);
            var tr = item.transform;
            if (k < LandAt)
            {
                // Pop: an elastic swell to 115% then back to 100%, hovering; then a gravity fall.
                float p = Mathf.Clamp01(k / PopEnd);
                float swell = p < 1f ? Mathf.Sin(p * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(p * Mathf.PI)) : 1f;
                float f = Mathf.Clamp01((k - PopEnd) / (LandAt - PopEnd));
                tr.localPosition = rest + Vector3.up * dropHeight * (1f - f * f);
                // Stretched a touch while falling fast.
                float stretch = 1f + 0.08f * f * f;
                tr.localScale = new Vector3(swell / Mathf.Sqrt(stretch), swell * stretch, swell / Mathf.Sqrt(stretch));
                return;
            }
            // Landed: a damped squash-and-stretch wobble, volume kept roughly constant.
            float s = (k - LandAt) / (1f - LandAt);
            float squash = 1f - 0.16f * Mathf.Exp(-5f * s) * Mathf.Cos(s * Mathf.PI * 3.2f);
            if (k >= 1f)
                squash = 1f;
            float wide = 1f / Mathf.Sqrt(squash);
            tr.localPosition = rest;
            tr.localScale = new Vector3(wide, squash, wide);
        }
    }

    /// <summary>A ring of the palette's accent color that spreads across the floor and fades as a piece lands.</summary>
    class SpawnRing : MonoBehaviour
    {
        const int Points = 48;
        const float Seconds = 0.7f;

        static Material s_Material;

        LineRenderer m_Line;
        float m_From, m_To, m_Start;
        Color m_Color;

        public static void Play(FurnitureItem item)
        {
            if (item == null)
                return;
            var bounds = new Bounds(item.transform.position, Vector3.zero);
            foreach (var r in item.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            float radius = Mathf.Max(0.12f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            var go = new GameObject("Landing Ring");
            go.transform.position = new Vector3(bounds.center.x, item.transform.position.y + 0.005f, bounds.center.z);
            var ring = go.AddComponent<SpawnRing>();
            ring.m_From = radius * 0.8f;
            ring.m_To = radius * 1.6f + 0.2f;
            ring.m_Color = PaletteManager.Instance != null ? PaletteManager.Instance.Current.Get(PaletteSlot.Accent) : Color.white;
            ring.m_Start = Time.time;
        }

        void Start()
        {
            // The UI shader is always in the build and takes vertex colors.
            s_Material ??= new Material(Canvas.GetDefaultCanvasMaterial()) { name = "Landing Ring" };
            m_Line = gameObject.AddComponent<LineRenderer>();
            m_Line.sharedMaterial = s_Material;
            m_Line.loop = true;
            m_Line.useWorldSpace = false;
            m_Line.positionCount = Points;
            m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Line.receiveShadows = false;
            m_Line.alignment = LineAlignment.TransformZ;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lying flat, facing up
            Update();
        }

        void Update()
        {
            float t = (Time.time - m_Start) / Seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            float r = Mathf.Lerp(m_From, m_To, ease);
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                m_Line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            var c = m_Color;
            c.a = 0.85f * (1f - t);
            m_Line.startColor = m_Line.endColor = c;
            m_Line.widthMultiplier = Mathf.Lerp(0.03f, 0.008f, t);
        }
    }
}
