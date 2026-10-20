using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace PaletteRoom
{
    /// <summary>Home-mode catalog entries.</summary>
    public enum FurnitureKind
    {
        Sofa,
        Armchair,
        CoffeeTable,
        Rug,
        FloorLamp,
        SideTable,
        Bookshelf,
        Plant,
        WallArt,
        ThrowPillow,
    }

    /// <summary>
    /// Builds catalog pieces. Kinds with a prefab in the <see cref="FurnitureLibrary"/> use the scanned
    /// model; the rest are modelled here in a Scandinavian flat-pack style from rounded meshes and the
    /// library's linen, bouclé, paint and oak materials. Every root sits on the floor (local y = 0) and
    /// faces local +Z.
    /// </summary>
    public class FurnitureFactory
    {
        readonly FurnitureLibrary m_Library;
        readonly Material m_Fallback;

        /// <param name="library">Prefabs and materials; may be null, in which case everything is plain painted.</param>
        /// <param name="fallback">A URP Lit material referenced from the scene so the shader is included in builds.</param>
        public FurnitureFactory(FurnitureLibrary library, Material fallback)
        {
            m_Library = library;
            m_Fallback = fallback;
        }

        Material Upholstery => Pick(m_Library != null ? m_Library.upholstery : null);
        Material Rug => Pick(m_Library != null ? m_Library.rugWeave : null);
        Material Paint => Pick(m_Library != null ? m_Library.paint : null);
        Material Oak => Pick(m_Library != null ? m_Library.oak : null);
        Material Metal => Pick(m_Library != null ? m_Library.metal : null);
        Material Bulb => Pick(m_Library != null ? m_Library.bulb : null);

        Material Pick(Material m) => m != null ? m : m_Fallback;

        /// <summary>Builds the catalog's default size of <paramref name="kind"/>.</summary>
        public FurnitureItem Create(FurnitureKind kind, Transform parent) =>
            Create(kind, FurnitureCatalog.Get(kind).Default, parent);

        public FurnitureItem Create(FurnitureKind kind, FurnitureVariant variant, Transform parent)
        {
            var prefab = m_Library != null ? m_Library.PrefabFor(kind) : null;
            if (prefab != null)
                return FromPrefab(kind, variant, prefab, parent);

            var b = new Builder(kind, variant, FurnitureCatalog.Get(kind).displayName, parent);
            switch (kind)
            {
                case FurnitureKind.Sofa: Sofa(b, variant); break;
                case FurnitureKind.CoffeeTable: CoffeeTable(b, variant); break;
                case FurnitureKind.Rug: RugPiece(b, variant); break;
                case FurnitureKind.FloorLamp: FloorLamp(b); break;
                case FurnitureKind.Bookshelf: Bookshelf(b, variant); break;
                default:
                    Debug.LogError($"[PaletteRoom] No prefab or builder for {kind}; run Palette Room ▸ Rebuild Furniture Assets.");
                    Sofa(b, FurnitureCatalog.Get(FurnitureKind.Sofa).Default);
                    break;
            }
            return b.Finish(MountFor(kind));
        }

        static FurnitureMount MountFor(FurnitureKind kind)
        {
            switch (kind)
            {
                case FurnitureKind.WallArt: return FurnitureMount.Wall;
                case FurnitureKind.ThrowPillow: return FurnitureMount.Free;
                default: return FurnitureMount.Floor;
            }
        }

        FurnitureItem FromPrefab(FurnitureKind kind, FurnitureVariant variant, GameObject prefab, Transform parent)
        {
            var b = new Builder(kind, variant, FurnitureCatalog.Get(kind).displayName, parent);
            // Size variants scale the model, not the root, so the drop-in animation can still own root scale.
            var model = Object.Instantiate(prefab, b.Root.transform, false);
            model.name = prefab.name;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, variant.scale);
            return b.Finish(MountFor(kind));
        }

        // ---- code-built pieces -------------------------------------------------------------

        void Sofa(Builder b, FurnitureVariant v)
        {
            // A deep, boxy sofa with loose cushions and slim tapered oak legs. The chaise variant extends
            // the end seat on the right as seen from the front (the sitter's left) into a long lounging section.
            const float legs = 0.1f, arm = 0.17f, depth = 0.88f;
            float width = v.size.x;
            int seats = Mathf.Max(1, v.seats);
            float inner = width - 2f * arm;
            float pitch = inner / seats;
            float extra = v.chaise ? Mathf.Max(0f, v.size.z - depth) : 0f;

            var body = new List<Part>
            {
                Part.Box(new Vector3(0f, legs + 0.11f, 0f), new Vector3(width, 0.22f, depth), 0.04f),
                Part.Box(new Vector3(0f, legs + 0.43f, -depth / 2f + 0.08f), new Vector3(inner, 0.42f, 0.14f), 0.05f),
                Part.Box(new Vector3(-(width - arm) / 2f, legs + 0.24f, 0f), new Vector3(arm, 0.48f, depth), 0.06f),
                Part.Box(new Vector3((width - arm) / 2f, legs + 0.24f, 0f), new Vector3(arm, 0.48f, depth), 0.06f),
            };
            for (int i = 0; i < seats; i++)
            {
                float x = -inner / 2f + pitch * (i + 0.5f);
                bool chaiseSeat = extra > 0f && i == 0;
                float seatDepth = 0.64f + (chaiseSeat ? extra : 0f);
                body.Add(Part.Box(new Vector3(x, legs + 0.295f, 0.08f + (chaiseSeat ? extra / 2f : 0f)), new Vector3(pitch - 0.01f, 0.15f, seatDepth), 0.06f, 0.015f));
                body.Add(Part.Box(new Vector3(x, legs + 0.57f, -0.21f), new Vector3(pitch - 0.02f, 0.40f, 0.17f), 0.07f, 0.02f,
                    Quaternion.Euler(-12f, 0f, 0f)));
            }

            var legParts = new List<Part>(Legs(width / 2f - 0.07f, depth / 2f - 0.08f, legs, 0.022f, 0.015f));
            if (extra > 0f)
            {
                // Base under the chaise, flush with the arm on that end.
                float chaiseW = pitch + arm;
                float cx = -width / 2f + chaiseW / 2f;
                body.Add(Part.Box(new Vector3(cx, legs + 0.11f, depth / 2f + extra / 2f), new Vector3(chaiseW, 0.22f, extra + 0.02f), 0.04f));
                var leg = MeshKit.TaperedLeg(legs, 0.022f, 0.015f);
                legParts.Add(new Part(leg, new Vector3(-width / 2f + 0.07f, 0f, depth / 2f + extra - 0.08f), Quaternion.identity));
                legParts.Add(new Part(leg, new Vector3(-width / 2f + chaiseW - 0.07f, 0f, depth / 2f + extra - 0.08f), Quaternion.identity));

                // Two floor footprints keep the L's notch usable (a coffee table can tuck in).
                b.AddCollider(new Vector3(0f, 0.45f, 0f), new Vector3(width, 0.9f, depth));
                b.AddCollider(new Vector3(cx, (legs + 0.45f) / 2f, depth / 2f + extra / 2f), new Vector3(chaiseW, legs + 0.45f, extra));
            }
            b.Zone("Upholstery", ZoneColorSource.ItemRole, PaletteSlot.Dominant, Upholstery, body.ToArray());
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, legParts.ToArray());
        }

        void CoffeeTable(Builder b, FurnitureVariant v)
        {
            // Lacquered top and shelf on four tapered oak legs.
            float length = v.size.x, height = v.size.y, depth = v.size.z;
            const float top = 0.035f, inset = 0.05f;
            b.Zone("Top", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint,
                Part.Box(new Vector3(0f, height - top / 2f, 0f), new Vector3(length, top, depth), 0.012f));
            b.Zone("Shelf", ZoneColorSource.Slot, PaletteSlot.Neutral, Paint,
                Part.Box(new Vector3(0f, 0.17f, 0f), new Vector3(length - 2f * inset, 0.02f, depth - 2f * inset), 0.006f));
            b.Zone("Legs", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, Legs(length / 2f - inset, depth / 2f - inset, height - top, 0.024f, 0.017f));
        }

        void RugPiece(Builder b, FurnitureVariant v)
        {
            // Bouclé field with a contrasting bound edge.
            b.Zone("Field", ZoneColorSource.ItemRole, PaletteSlot.Dominant, Rug,
                Part.Box(new Vector3(0f, 0.007f, 0f), new Vector3(v.size.x - 0.1f, 0.014f, v.size.z - 0.1f), 0.006f));
            b.Zone("Border", ZoneColorSource.Slot, PaletteSlot.Neutral, Rug,
                Part.Box(new Vector3(0f, 0.005f, 0f), new Vector3(v.size.x, 0.01f, v.size.z), 0.004f));
        }

        void FloorLamp(Builder b)
        {
            // Tripod of oak legs under a linen drum shade.
            const float apex = 1.22f, spread = 0.28f;

            var shade = MeshKit.Lathe(new[] { new Vector2(0.22f, 0f), new Vector2(0.21f, 0.3f) }, 32, capEnds: false);
            b.Zone("Shade", ZoneColorSource.ItemRole, PaletteSlot.Accent, Upholstery,
                new Part(shade, new Vector3(0f, 1.3f, 0f), Quaternion.identity), doubleSided: true);

            var legs = new Part[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(a) * spread, 0f, Mathf.Sin(a) * spread);
                var dir = new Vector3(0f, apex, 0f) - foot;
                legs[i] = new Part(MeshKit.TaperedLeg(dir.magnitude, 0.016f, 0.011f), foot, Quaternion.FromToRotation(Vector3.up, dir));
            }
            b.Zone("Tripod", ZoneColorSource.Fixed, PaletteSlot.Neutral, Oak, legs);
            b.Zone("Fittings", ZoneColorSource.Fixed, PaletteSlot.Neutral, Metal,
                new Part(MeshKit.Lathe(new[] { new Vector2(0.014f, 0f), new Vector2(0.014f, 0.17f) }, 12), new Vector3(0f, apex - 0.02f, 0f), Quaternion.identity),
                new Part(MeshKit.Lathe(new[] { new Vector2(0.03f, 0f), new Vector2(0.03f, 0.04f) }, 16), new Vector3(0f, 1.36f, 0f), Quaternion.identity));
            b.Zone("Bulb", ZoneColorSource.Fixed, PaletteSlot.Neutral, Bulb,
                new Part(Sphere(), new Vector3(0f, 1.43f, 0f), Quaternion.identity, Vector3.one * 0.07f));
        }

        void Bookshelf(Builder b, FurnitureVariant v)
        {
            // A cube shelf (33.5 cm cells, 3.5 cm lacquered panels) about half-filled with books.
            const float cell = 0.335f, t = 0.035f, d = 0.39f, r = 0.004f;
            int cols = Mathf.Max(1, v.cells.x), rows = Mathf.Max(1, v.cells.y);
            float w = cols * cell + (cols + 1) * t, h = rows * cell + (rows + 1) * t;

            var frame = new List<Part>
            {
                Part.Box(new Vector3(-(w - t) / 2f, h / 2f, 0f), new Vector3(t, h, d), r),
                Part.Box(new Vector3((w - t) / 2f, h / 2f, 0f), new Vector3(t, h, d), r),
            };
            for (int c = 1; c < cols; c++)
                frame.Add(Part.Box(new Vector3(-w / 2f + c * (cell + t) + t / 2f, h / 2f, 0f), new Vector3(t, h - 2 * t, d - 0.002f), r));
            for (int i = 0; i <= rows; i++)
                frame.Add(Part.Box(new Vector3(0f, t / 2f + i * (cell + t), 0f), new Vector3(w - 2 * t + 0.002f, t, d), r));
            b.Zone("Frame", ZoneColorSource.ItemRole, PaletteSlot.Secondary, Paint, frame.ToArray());
            b.Zone("Back Panel", ZoneColorSource.Slot, PaletteSlot.Neutral, Paint,
                Part.Box(new Vector3(0f, h / 2f, -d / 2f + 0.006f), new Vector3(w - 2 * t, h - 2 * t, 0.008f), 0.002f));

            // Books in a checkerboard of cells; the last book in each row leans on its neighbour.
            var books = new List<Part>();
            float[] heights = { 0.24f, 0.21f, 0.27f, 0.2f, 0.23f, 0.26f, 0.19f, 0.25f };
            int n = 0, filled = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if ((row + col) % 2 != 0)
                        continue;
                    float left = -w / 2f + t + col * (cell + t);
                    float floorY = t + row * (cell + t);
                    float x = left + 0.02f;
                    int count = 4 + (filled++ % 3);
                    for (int i = 0; i < count; i++, n++)
                    {
                        float bh = heights[n % heights.Length];
                        float thick = 0.03f + (n % 3) * 0.008f;
                        books.Add(Part.Box(new Vector3(x + thick / 2f, floorY + bh / 2f, 0.02f), new Vector3(thick, bh, 0.22f), 0.003f));
                        x += thick + 0.003f;
                    }
                    const float lean = 0.22f;
                    books.Add(Part.Box(new Vector3(x + 0.05f, floorY + lean / 2f * Mathf.Cos(0.3f), 0.02f), new Vector3(0.032f, lean, 0.21f), 0.003f,
                        0f, Quaternion.Euler(0f, 0f, -17f)));
                }
            }
            b.Zone("Books", ZoneColorSource.Slot, PaletteSlot.Accent, Paint, books.ToArray());
        }

        // ---- geometry helpers -------------------------------------------------------------

        readonly struct Part
        {
            public readonly Mesh mesh;
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly Vector3 scale;

            public Part(Mesh mesh, Vector3 position, Quaternion rotation, Vector3? scale = null)
            {
                this.mesh = mesh;
                this.position = position;
                this.rotation = rotation;
                this.scale = scale ?? Vector3.one;
            }

            public static Part Box(Vector3 centre, Vector3 size, float radius, float puff = 0f, Quaternion? rotation = null) =>
                new Part(MeshKit.RoundedBox(size, radius, puff), centre, rotation ?? Quaternion.identity);
        }

        static Part[] Legs(float halfX, float halfZ, float height, float topRadius, float bottomRadius)
        {
            var leg = MeshKit.TaperedLeg(height, topRadius, bottomRadius);
            return new[]
            {
                new Part(leg, new Vector3(halfX, 0f, halfZ), Quaternion.identity),
                new Part(leg, new Vector3(-halfX, 0f, halfZ), Quaternion.identity),
                new Part(leg, new Vector3(halfX, 0f, -halfZ), Quaternion.identity),
                new Part(leg, new Vector3(-halfX, 0f, -halfZ), Quaternion.identity),
            };
        }

        static Mesh s_Sphere;

        static Mesh Sphere()
        {
            if (s_Sphere == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s_Sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying)
                    Object.Destroy(tmp);
                else
                    Object.DestroyImmediate(tmp);
            }
            return s_Sphere;
        }

        class Builder
        {
            readonly FurnitureKind m_Kind;
            readonly FurnitureVariant m_Variant;

            public GameObject Root { get; }

            public Builder(FurnitureKind kind, FurnitureVariant variant, string name, Transform parent)
            {
                m_Kind = kind;
                m_Variant = variant;
                Root = new GameObject(name);
                Root.transform.SetParent(parent, false);
            }

            /// <summary>Adds an explicit collision box; pieces without one get a single box around their bounds.</summary>
            public void AddCollider(Vector3 centre, Vector3 size)
            {
                var box = Root.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = size;
            }

            public void Zone(string zoneName, ZoneColorSource source, PaletteSlot slot, Material template, params Part[] parts) =>
                Zone(zoneName, source, slot, template, parts, false);

            public void Zone(string zoneName, ZoneColorSource source, PaletteSlot slot, Material template, Part part, bool doubleSided) =>
                Zone(zoneName, source, slot, template, new[] { part }, doubleSided);

            void Zone(string zoneName, ZoneColorSource source, PaletteSlot slot, Material template, Part[] parts, bool doubleSided)
            {
                var zoneGo = new GameObject(zoneName);
                zoneGo.transform.SetParent(Root.transform, false);
                var targets = new PaintableZone.Target[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    var go = new GameObject($"{zoneName} {i + 1}", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(zoneGo.transform, false);
                    go.transform.localPosition = parts[i].position;
                    go.transform.localRotation = parts[i].rotation;
                    go.transform.localScale = parts[i].scale;
                    go.GetComponent<MeshFilter>().sharedMesh = parts[i].mesh;
                    targets[i] = new PaintableZone.Target(go.GetComponent<MeshRenderer>());
                }
                var material = new Material(template) { name = $"{Root.name} {zoneName}" };
                if (doubleSided)
                    material.SetFloat("_Cull", 0f);
                zoneGo.AddComponent<PaintableZone>().Configure(zoneName, source, slot, material, targets);
            }

            public FurnitureItem Finish(FurnitureMount mount)
            {
                var entry = FurnitureCatalog.Get(m_Kind);
                if (Root.GetComponent<BoxCollider>() == null)
                {
                    var bounds = LocalBounds();
                    AddCollider(bounds.center, bounds.size);
                }

                // Everything is added while inactive so Awake/OnEnable see a fully configured item:
                // the palette registers the right role and the grab interactable picks up the collider.
                Root.SetActive(false);
                var item = Root.AddComponent<FurnitureItem>();
                item.Configure(m_Kind, m_Variant.id, $"{entry.kind}_{m_Variant.id}".ToLowerInvariant(), entry.displayName, entry.role, mount);
                MakeGrabbable(Root);
                Root.SetActive(true);
                return item;
            }

            static void MakeGrabbable(GameObject root)
            {
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var grab = root.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
                grab.trackScale = false;
                grab.useDynamicAttach = true;                      // hold it where you grabbed it
                grab.farAttachMode = InteractableFarAttachMode.Far; // a ray grab keeps the sofa where it is
                grab.selectMode = InteractableSelectMode.Multiple; // second hand rotates

                var general = root.AddComponent<XRGeneralGrabTransformer>();
                general.allowOneHandedScaling = false;
                general.allowTwoHandedScaling = false;
                root.AddComponent<FurnitureGrabTransformer>();
            }

            Bounds LocalBounds()
            {
                var renderers = Root.GetComponentsInChildren<Renderer>();
                var toLocal = Root.transform.worldToLocalMatrix;
                var bounds = new Bounds();
                bool first = true;
                foreach (var r in renderers)
                {
                    var wb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var local = toLocal.MultiplyPoint3x4(corner);
                        if (first)
                        {
                            bounds = new Bounds(local, Vector3.zero);
                            first = false;
                        }
                        else
                        {
                            bounds.Encapsulate(local);
                        }
                    }
                }
                return bounds;
            }
        }
    }
}
