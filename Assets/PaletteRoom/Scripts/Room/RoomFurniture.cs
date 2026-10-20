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
    public class RoomFurniture : MonoBehaviour
    {
        [Tooltip("Models and materials for the catalog (Palette Room ▸ Rebuild Furniture Assets).")]
        [SerializeField] FurnitureLibrary library;
        [Tooltip("Plain URP Lit material, used if the library is missing anything.")]
        [SerializeField] Material baseMaterial;
        [Tooltip("Distance from the user to the sample room's centre line.")]
        [SerializeField] float distanceFromUser = 1.9f;
        [Tooltip("Distance in front of the user where shopping-list items appear.")]
        [SerializeField] float addDistance = 1.3f;
        [SerializeField] float dropHeight = 0.35f;
        [SerializeField] float dropDuration = 0.45f;
        [SerializeField] float dropStagger = 0.06f;

        struct Placement
        {
            public FurnitureKind kind;
            public Vector3 position;
            public float yaw;

            public Placement(FurnitureKind kind, float x, float z, float yaw, float y = 0f)
            {
                this.kind = kind;
                position = new Vector3(x, y, z);
                this.yaw = yaw;
            }
        }

        // Pieces face the user (yaw 180) unless angled toward the seating area.
        // Pillows are listed back-to-back so the accent/highlight alternation gives them different colors.
        static readonly Placement[] SampleLayout =
        {
            new Placement(FurnitureKind.Rug, 0f, 0.3f, 180f),
            new Placement(FurnitureKind.Sofa, 0f, 1.1f, 180f),
            new Placement(FurnitureKind.CoffeeTable, 0f, 0.25f, 180f),
            new Placement(FurnitureKind.Armchair, -1.35f, 0.2f, 120f),
            new Placement(FurnitureKind.Bookshelf, 2.1f, 0.4f, -120f),
            new Placement(FurnitureKind.SideTable, 1.42f, 1.15f, 180f),
            new Placement(FurnitureKind.FloorLamp, -1.25f, 1.3f, 180f),
            new Placement(FurnitureKind.Plant, -2.15f, 1.25f, 180f),
            new Placement(FurnitureKind.ThrowPillow, -0.62f, 1.14f, 180f, 0.47f),
            new Placement(FurnitureKind.ThrowPillow, 0.62f, 1.14f, 180f, 0.47f),
        };

        readonly List<FurnitureItem> m_Items = new List<FurnitureItem>();
        FurnitureFactory m_Factory;
        Transform m_Root;

        public IReadOnlyList<FurnitureItem> Items => m_Items;
        public FurnitureLibrary Library => library;

        /// <summary>Raised whenever pieces are added or removed.</summary>
        public event Action Changed;

        FurnitureFactory Factory => m_Factory ?? (m_Factory = new FurnitureFactory(library, baseMaterial));

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

        /// <summary>Replaces everything with the pre-arranged living room, centred in front of the user.</summary>
        public void SpawnSampleRoom(Transform head, float floorY)
        {
            Clear();
            var root = EnsureRoot(head, floorY);
            var forward = FlatForward(head);
            var centre = head.position + forward * distanceFromUser;
            root.SetPositionAndRotation(new Vector3(centre.x, floorY, centre.z), Quaternion.LookRotation(forward, Vector3.up));

            var added = new List<FurnitureItem>();
            FurnitureItem bookshelf = null;
            foreach (var p in SampleLayout)
            {
                var item = Factory.Create(p.kind, root);
                item.transform.localPosition = p.position;
                item.transform.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
                if (p.kind == FurnitureKind.ThrowPillow)
                    item.transform.localRotation *= Quaternion.Euler(-15f, 0f, 0f); // lean back into the sofa
                if (p.kind == FurnitureKind.Bookshelf)
                    bookshelf = item;
                // Safety net: if a model's size ever makes the layout collide, nudge to the nearest free spot.
                var pose = item.transform;
                if (item.BlocksFloor && !IsFree(item, pose.position, pose.rotation)
                    && TryFindFreeSpot(item, pose.position, pose.rotation, head.position, out var spot))
                    pose.position = spot;
                added.Add(item);
                m_Items.Add(item);
            }

            // Wall art leans on top of the bookshelf, tilted back against an imagined wall.
            var art = Factory.Create(FurnitureKind.WallArt, root);
            var shelfBox = bookshelf.GetComponent<BoxCollider>();
            float shelfTop = shelfBox.center.y + shelfBox.size.y / 2f;
            float shelfBack = shelfBox.center.z - shelfBox.size.z / 2f;
            art.transform.SetPositionAndRotation(
                bookshelf.transform.TransformPoint(new Vector3(0f, shelfTop, shelfBack + 0.08f)),
                bookshelf.transform.rotation * Quaternion.Euler(-8f, 0f, 0f));
            added.Add(art);
            m_Items.Add(art);

            DropIn(added);
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds one piece in front of the user, facing them. Floor-standing pieces go to the nearest
        /// spot where they don't intersect anything already in the room.
        /// </summary>
        public FurnitureItem Add(FurnitureKind kind, string variantId, Transform head, float floorY)
        {
            var root = EnsureRoot(head, floorY);
            var forward = FlatForward(head);
            var rotation = Quaternion.LookRotation(-forward, Vector3.up);
            var preferred = head.position + forward * addDistance;
            float height = kind == FurnitureKind.WallArt ? 1.2f : 0f; // hung at picture height

            var item = Factory.Create(kind, FurnitureCatalog.Get(kind).Variant(variantId), root);
            var spot = new Vector3(preferred.x, floorY + height, preferred.z);
            if (item.BlocksFloor && !TryFindFreeSpot(item, spot, rotation, head.position, out spot))
                Debug.LogWarning($"[PaletteRoom] No free floor space found for {item.DisplayName}; placed in front of the user.");
            item.transform.SetPositionAndRotation(spot, rotation);
            m_Items.Add(item);
            DropIn(new List<FurnitureItem> { item });
            Changed?.Invoke();
            return item;
        }

        /// <summary>True if <paramref name="item"/> at this pose would not intersect another floor-standing piece.</summary>
        public bool IsFree(FurnitureItem item, Vector3 position, Quaternion rotation)
        {
            if (!item.BlocksFloor)
                return true;
            var footprints = item.FootprintsAt(position, rotation);
            foreach (var other in m_Items)
            {
                if (other == null || other == item || !other.BlocksFloor)
                    continue;
                if (FurnitureItem.Overlap(footprints, other.CurrentFootprints))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Searches outward from <paramref name="preferred"/> in rings for the closest free spot that
        /// is neither on top of the user nor out of reach.
        /// </summary>
        bool TryFindFreeSpot(FurnitureItem item, Vector3 preferred, Quaternion rotation, Vector3 userPosition, out Vector3 spot)
        {
            const float step = 0.15f, maxRadius = 3f, minFromUser = 0.6f, maxFromUser = 4f;
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
                    if (IsFree(item, candidate, rotation))
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
        }

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

        IEnumerator DropRoutine(List<FurnitureItem> items)
        {
            var rests = new Vector3[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                rests[i] = items[i].transform.localPosition;
                items[i].transform.localPosition = rests[i] + Vector3.up * dropHeight;
                items[i].transform.localScale = Vector3.zero;
            }

            float total = dropDuration + dropStagger * (items.Count - 1);
            for (float t = 0f; t <= total; t += Time.deltaTime)
            {
                for (int i = 0; i < items.Count; i++)
                    Pose(items[i], (t - i * dropStagger) / dropDuration, rests[i]);
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
            float fall = 1f - (1f - k) * (1f - k); // ease-out
            float settle = 1f + Mathf.Sin(k * Mathf.PI) * 0.04f;
            var tr = item.transform;
            tr.localPosition = rest + Vector3.up * dropHeight * (1f - fall);
            tr.localScale = Vector3.one * Mathf.Min(1f, k * 3f) * settle;
        }
    }
}
