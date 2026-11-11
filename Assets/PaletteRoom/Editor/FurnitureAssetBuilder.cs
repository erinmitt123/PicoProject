using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PaletteRoom.EditorTools
{
    /// <summary>
    /// Turns the downloaded CC0 models and textures (see Models/CREDITS.md) into what the app uses:
    /// grayscale "tintable" textures for recolorable surfaces, URP Lit materials, one prefab per scanned
    /// piece with its paintable zones, and the <see cref="FurnitureLibrary"/> asset. Safe to re-run.
    /// </summary>
    public static class FurnitureAssetBuilder
    {
        const string Root = "Assets/PaletteRoom";
        const string Models = Root + "/Models";
        const string Textures = Root + "/Textures";
        const string MaterialDir = Root + "/Materials/Furniture";
        const string PrefabDir = Root + "/Prefabs";
        const string LibraryPath = Root + "/FurnitureLibrary.asset";

        // Model yaw (degrees) that turns each scan to face +Z, and a uniform scale where the scan is
        // larger than the piece should be.
        static readonly Dictionary<string, (float yaw, float scale)> ModelFit = new Dictionary<string, (float, float)>
        {
            { "modern_arm_chair_01", (0f, 1f) },
            { "side_table_01", (0f, 1f) },
            { "potted_plant_02", (0f, 1f) },
            { "hanging_picture_frame_01", (0f, 1f) }, // true A1 (59.4 × 84.1 cm); smaller prints are size variants
            { "throw_pillows_01", (0f, 1f) },
            { "Sofa_01", (0f, 1f) },
            { "mid_century_lounge_chair", (0f, 0.83f) },   // the scan is ~20% oversized
            { "Ottoman_01", (0f, 1f) },
            { "modern_coffee_table_01", (0f, 1f) },
            { "coffee_table_round_01", (0f, 0.75f) },      // 130 cm scan → 98 cm, 37 cm high
            { "ClassicNightstand_01", (0f, 0.9f) },
            { "ClassicConsole_01", (0f, 0.82f) },          // to a 78 cm console height
            { "vintage_wooden_drawer_01", (0f, 0.7f) },    // 172 cm scan → 120 cm; the variant also trims depth
            { "modern_wooden_cabinet", (0f, 0.74f) },      // 244 cm scan → 180 cm, 50 cm high
            { "wooden_display_shelves_01", (0f, 1f) },
            { "dining_chair_02", (0f, 1f) },
            { "round_wooden_table_01", (0f, 0.75f) },      // to a 75 cm dining height
            { "ornate_mirror_01", (0f, 1.3f) },            // 63 × 97 cm
            { "ceramic_vase_01", (0f, 1f) },
            { "potted_plant_04", (0f, 1f) },
        };

        [MenuItem("Palette Room/Rebuild Furniture Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            ConfigureTextureImports();

            // Shared materials for the code-built pieces.
            var upholstery = Lit("Upholstery Linen", Tint($"{Textures}/rough_linen_diff_1k.png", 0.8f), Tex($"{Textures}/rough_linen_nor_gl_1k.png"), Color.white, 0.18f, tiling: 2f);
            var rug = Lit("Rug Boucle", Tint($"{Textures}/wool_boucle_diff_1k.png", 0.8f), Tex($"{Textures}/wool_boucle_nor_gl_1k.png"), Color.white, 0.08f, tiling: 2.5f);
            var paint = Lit("Satin Lacquer", null, null, Color.white, 0.55f);
            // The scan is a mid-tone oak; lift it toward the pale birch/oak typical of flat-pack furniture.
            var oak = Lit("White Oak", Brightened($"{Textures}/white_oak_veneer_diff_1k.png", 1.4f), Tex($"{Textures}/white_oak_veneer_nor_gl_1k.png"), Color.white, 0.38f, tiling: 1.5f);
            var metal = Metal();
            var bulb = Lit("Warm Bulb", null, null, new Color(1f, 0.93f, 0.8f), 0.8f);
            bulb.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.55f) * 2.2f);
            ApplyKeywords(bulb);

            var library = AssetDatabase.LoadAssetAtPath<FurnitureLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FurnitureLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.upholstery = upholstery;
            library.rugWeave = rug;
            library.paint = paint;
            library.oak = oak;
            library.metal = metal;
            library.bulb = bulb;
            library.prefabs = new[]
            {
                Entry(FurnitureKind.Armchair, BuildArmchair()),
                Entry(FurnitureKind.SideTable, BuildSideTable()),
                Entry(FurnitureKind.Plant, BuildPlant()),
                Entry(FurnitureKind.ThrowPillow, BuildPillow()),
                Entry(FurnitureKind.Settee, BuildSettee()),
                Entry(FurnitureKind.LoungeChair, BuildLoungeChair()),
                Entry(FurnitureKind.LeatherOttoman, BuildWhole("Leather Ottoman", "Ottoman_01", "Cover", 0.5f, 0.35f)),
                Entry(FurnitureKind.InlayCoffeeTable, BuildInlayCoffeeTable()),
                Entry(FurnitureKind.RoundCoffeeTable, BuildRoundCoffeeTable()),
                Entry(FurnitureKind.CarvedNightstand, BuildWhole("Carved Nightstand", "ClassicNightstand_01", "Finish", 0.75f, 0.4f)),
                Entry(FurnitureKind.CarvedConsole, BuildWhole("Carved Console", "ClassicConsole_01", "Finish", 0.75f, 0.4f)),
                Entry(FurnitureKind.ApothecaryChest, BuildApothecaryChest()),
                Entry(FurnitureKind.SlattedMediaUnit, BuildSlattedMediaUnit()),
                Entry(FurnitureKind.CubeShelves, BuildCubeShelves()),
                Entry(FurnitureKind.TuftedDiningChair, BuildTuftedDiningChair()),
                Entry(FurnitureKind.PedestalTable, BuildPedestalTable()),
                Entry(FurnitureKind.ArchedMirror, BuildArchedMirror()),
                Entry(FurnitureKind.CeramicVase, BuildWhole("Ceramic Vase", "ceramic_vase_01", "Glaze", 0.6f, 0.75f)),
                Entry(FurnitureKind.Succulent, BuildSucculent()),
            };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            library.icons = BakeIcons(library);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PaletteRoom] Furniture assets rebuilt → {LibraryPath}");
        }

        // ---- icons ------------------------------------------------------------------------

        const string IconDir = Root + "/Icons";
        const int IconSize = 128;
        const int IconLayer = 31; // render in isolation from whatever scene is open

        /// <summary>
        /// Renders each catalog piece, in the default palette, to a small transparent sprite. Lists show
        /// these flat images instead of live 3D models, so a row costs one quad and one 128² texture.
        /// </summary>
        static FurnitureLibrary.IconEntry[] BakeIcons(FurnitureLibrary library)
        {
            Directory.CreateDirectory(IconDir);
            var fallback = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/FurnitureBase.mat");
            var factory = new FurnitureFactory(library, fallback);
            var palette = PalettePresets.Default;
            var stage = new GameObject("Icon Stage") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(0f, -500f, 0f);

            var lightGo = new GameObject("Icon Key Light") { hideFlags = HideFlags.HideAndDontSave };
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.color = new Color(1f, 0.97f, 0.92f);
            key.cullingMask = 1 << IconLayer;
            key.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            var cam = new GameObject("Icon Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            cam.cullingMask = 1 << IconLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            var rt = new RenderTexture(IconSize * 2, IconSize * 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var entries = new List<FurnitureLibrary.IconEntry>();
            try
            {
                foreach (var entry in FurnitureCatalog.Home)
                foreach (var variant in entry.variants)
                {
                    var item = factory.Create(entry.kind, variant, stage.transform);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                    TintForIcon(item, palette);
                    foreach (var t in item.GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer = IconLayer;

                    // Three-quarter view from front-right and slightly above, framed to the piece's bounds.
                    var bounds = WorldBounds(item);
                    float radius = bounds.extents.magnitude;
                    var dir = new Vector3(0.45f, 0.42f, 0.79f); // right, up, in front (pieces face +Z)
                    float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                    cam.transform.position = bounds.center + dir.normalized * distance;
                    cam.transform.LookAt(bounds.center);

                    cam.targetTexture = rt;
                    cam.Render();
                    var path = $"{IconDir}/{entry.kind}_{variant.id}.png";
                    File.WriteAllBytes(path, ReadDownsampled(rt));
                    cam.targetTexture = null;
                    Object.DestroyImmediate(item.gameObject);

                    AssetDatabase.ImportAsset(path);
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.maxTextureSize = IconSize;
                        importer.SaveAndReimport();
                    }
                    entries.Add(new FurnitureLibrary.IconEntry { kind = entry.kind, variantId = variant.id, icon = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
                }
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(cam.gameObject);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(stage);
            }
            return entries.ToArray();
        }

        /// <summary>Colors the paintable zones with temporary material copies (zones don't instance in edit mode).</summary>
        static void TintForIcon(FurnitureItem item, ColorPalette palette)
        {
            foreach (var zone in item.Zones)
            {
                if (!zone.IsPaintable)
                    continue;
                var slot = zone.Source == ZoneColorSource.Slot ? zone.Slot
                    : item.Role == FurnitureRole.Dominant ? PaletteSlot.Dominant
                    : item.Role == FurnitureRole.Secondary ? PaletteSlot.Secondary
                    : PaletteSlot.Accent;
                foreach (var t in zone.Targets)
                {
                    var mats = t.renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (t.materialIndex >= 0 && i != t.materialIndex)
                            continue;
                        mats[i] = new Material(mats[i]) { hideFlags = HideFlags.HideAndDontSave };
                        mats[i].SetColor("_BaseColor", palette.Get(slot));
                    }
                    t.renderer.sharedMaterials = mats;
                }
            }
        }

        static Bounds WorldBounds(FurnitureItem item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Reads the 2× render back and box-filters it to the icon size for clean edges.</summary>
        static byte[] ReadDownsampled(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var big = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            big.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            big.Apply();
            RenderTexture.active = prev;

            var src = big.GetPixels32();
            var dst = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            var c = src[(y * 2 + dy) * rt.width + x * 2 + dx];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                        }
                    }
                    dst[y * IconSize + x] = new Color32((byte)(r / 4), (byte)(g / 4), (byte)(b / 4), (byte)(a / 4));
                }
            }
            Object.DestroyImmediate(big);
            var small = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            small.SetPixels32(dst);
            small.Apply();
            var png = small.EncodeToPNG();
            Object.DestroyImmediate(small);
            return png;
        }

        static FurnitureLibrary.PrefabEntry Entry(FurnitureKind kind, GameObject prefab) =>
            new FurnitureLibrary.PrefabEntry { kind = kind, prefab = prefab };

        // ---- prefabs ----------------------------------------------------------------------

        static GameObject BuildArmchair()
        {
            const string id = "modern_arm_chair_01";
            var (root, model) = Begin("Armchair", id);
            var r = model.GetComponentInChildren<Renderer>();
            var cushions = Lit("Armchair Cushions", Tint(ModelTex(id, "pillow_diff_1k.png"), 0.45f), Tex(ModelTex(id, "pillow_nor_gl_1k.png")), Color.white, 0.2f);
            var frame = Lit("Armchair Frame", Tex(ModelTex(id, "legs_diff_1k.png")), Tex(ModelTex(id, "legs_nor_gl_1k.png")), Color.white, 0.4f);
            int pillowSlot = SlotOf(r, "pillow"), legsSlot = SlotOf(r, "legs");
            SetSlot(r, pillowSlot, cushions);
            SetSlot(r, legsSlot, frame);
            Zone(root, "Cushions", ZoneColorSource.ItemRole, new PaintableZone.Target(r, pillowSlot));
            Zone(root, "Frame", ZoneColorSource.Fixed, new PaintableZone.Target(r, legsSlot));
            return Save(root, "Armchair");
        }

        static GameObject BuildSideTable()
        {
            const string id = "side_table_01";
            var (root, model) = Begin("Side Table", id);
            var r = model.GetComponentInChildren<Renderer>();
            // Painted-oak finish: the grain still shows through the palette color.
            var finish = Lit("Side Table Painted Oak", Tint(ModelTex(id, "diff_1k.jpg"), 0.7f), Tex(ModelTex(id, "nor_gl_1k.exr")), Color.white, 0.42f);
            SetSlot(r, -1, finish);
            Zone(root, "Finish", ZoneColorSource.ItemRole, new PaintableZone.Target(r));
            return Save(root, "Side Table");
        }

        static GameObject BuildPlant()
        {
            const string id = "potted_plant_02";
            var (root, model) = Begin("Plant", id);
            Renderer pot = null, dirt = null, leaves = null;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name.EndsWith("_pot")) pot = r;
                else if (r.name.EndsWith("_dirt")) dirt = r;
                else if (r.name.EndsWith("_leaves")) leaves = r;
            }
            var potTex = ModelTex(id, "pot_diff_1k.png");
            var potNormal = Tex(ModelTex(id, "pot_nor_gl_1k.png"));
            SetSlot(pot, -1, Lit("Plant Pot", Tint(potTex, 0.75f), potNormal, Color.white, 0.5f));
            // The soil shares the pot's texture atlas, so it keeps the untinted original.
            SetSlot(dirt, -1, Lit("Plant Soil", Tex(potTex), potNormal, Color.white, 0.1f));
            var leafMat = Lit("Plant Leaves", WithAlpha(ModelTex(id, "leaves_diff_1k.png"), ModelTex(id, "leaves_alpha_1k.png")),
                Tex(ModelTex(id, "leaves_nor_gl_1k.png")), Color.white, 0.3f);
            leafMat.SetFloat("_AlphaClip", 1f);
            leafMat.SetFloat("_Cutoff", 0.45f);
            leafMat.SetFloat("_Cull", 0f);
            ApplyKeywords(leafMat);
            SetSlot(leaves, -1, leafMat);
            Zone(root, "Pot", ZoneColorSource.ItemRole, new PaintableZone.Target(pot));
            // Soil and leaves keep their natural look; only the pot is offered for recoloring.
            Zone(root, "Soil", ZoneColorSource.Fixed, new PaintableZone.Target(dirt)).SetAllowOverride(false);
            Zone(root, "Leaves", ZoneColorSource.Fixed, new PaintableZone.Target(leaves)).SetAllowOverride(false);
            return Save(root, "Plant");
        }

        static GameObject BuildPillow()
        {
            const string id = "throw_pillows_01";
            var (root, model) = Begin("Throw Pillow", id);
            Renderer keep = null;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name.EndsWith("pillow01"))
                    keep = r;
                else
                    Object.DestroyImmediate(r.gameObject); // one pillow per catalog item
            }
            // The scan's pillow faces +Z but sits rolled like a diamond; square it up, then centre it on the root.
            keep.transform.rotation = root.transform.rotation * SquareUp(keep.GetComponent<MeshFilter>().sharedMesh, keep.transform.lossyScale);
            var b = keep.bounds;
            keep.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            // The zigzag print is softened so the palette color dominates.
            SetSlot(keep, -1, Lit("Throw Pillow Cover", Tint(ModelTex(id, "diff_1k.jpg"), 0.45f), Tex(ModelTex(id, "nor_gl_1k.exr")), Color.white, 0.15f));
            Zone(root, "Cover", ZoneColorSource.ItemRole, new PaintableZone.Target(keep));
            return Save(root, "Throw Pillow");
        }

        // ---- scanned pieces, round two (Poly Haven) -----------------------------------------
        // Each of these scans is one mesh on one texture atlas. Where a piece has parts that should
        // behave differently (fabric vs. wood frame, mirror glass vs. gilt frame), SplitByTexture sorts
        // its triangles into two sub-meshes by what the atlas shows under them.

        /// <summary>A piece painted all over: one tinted material, one zone following the item's role.</summary>
        static GameObject BuildWhole(string name, string id, string zone, float detail, float smoothness)
        {
            var (root, model) = Begin(name, id);
            var finish = Lit($"{name} Finish", Tint(ModelTex(id, "diff_1k.jpg"), detail), Tex(ModelTex(id, "nor_gl_1k.exr")), Color.white, smoothness);
            var targets = new List<PaintableZone.Target>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                SetSlot(r, -1, finish);
                targets.Add(new PaintableZone.Target(r));
            }
            Zone(root, zone, ZoneColorSource.ItemRole, targets.ToArray());
            return Save(root, name);
        }

        /// <summary>
        /// Two-part piece on a split mesh: sub-mesh 0 (where <paramref name="painted"/> holds) gets the tinted
        /// material and follows the item's role; sub-mesh 1 keeps the scan's own texture and stays as it is.
        /// </summary>
        static GameObject BuildSplit(string name, string id, System.Func<Color, bool> painted, string paintedZone, float detail,
            float smoothness, string naturalZone, float naturalSmoothness)
        {
            var (root, model) = Begin(name, id);
            var r = model.GetComponentInChildren<Renderer>();
            SplitByTexture(id, r, ModelTex(id, "diff_1k.jpg"), painted);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            r.sharedMaterials = new[]
            {
                Lit($"{name} {paintedZone}", Tint(ModelTex(id, "diff_1k.jpg"), detail), normal, Color.white, smoothness),
                Lit($"{name} {naturalZone}", Tex(ModelTex(id, "diff_1k.jpg")), normal, Color.white, naturalSmoothness),
            };
            Zone(root, paintedZone, ZoneColorSource.ItemRole, new PaintableZone.Target(r, 0));
            Zone(root, naturalZone, ZoneColorSource.Fixed, new PaintableZone.Target(r, 1)).SetAllowOverride(false);
            return Save(root, name);
        }

        static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static float Sat(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out _);
            return s;
        }

        // Pale linen upholstery on a dark carved frame.
        static GameObject BuildSettee() =>
            BuildSplit("Settee", "Sofa_01", c => Lum(c) > 0.35f && Sat(c) < 0.3f, "Upholstery", 0.6f, 0.15f, "Frame", 0.45f);

        // Dark leather cushions; the bent-ply shell and the swivel base stay natural.
        static GameObject BuildLoungeChair() =>
            BuildSplit("Swivel Lounge Chair", "mid_century_lounge_chair", c => Lum(c) < 0.36f && Sat(c) > 0.3f, "Cushions", 0.5f, 0.35f, "Frame", 0.45f);

        // The grey concrete top takes the color; the oak frame stays oak.
        static GameObject BuildInlayCoffeeTable() =>
            BuildSplit("Inlay Coffee Table", "modern_coffee_table_01", c => Sat(c) < 0.3f, "Top", 0.7f, 0.35f, "Frame", 0.4f);

        // The pale stone top takes the color; the black bent-steel legs stay black.
        static GameObject BuildRoundCoffeeTable() =>
            BuildSplit("Round Coffee Table", "coffee_table_round_01", c => Lum(c) > 0.45f, "Top", 0.6f, 0.5f, "Legs", 0.5f);

        // Leather seat and back; the near-black legs stay as they are.
        static GameObject BuildTuftedDiningChair() =>
            BuildSplit("Tufted Dining Chair", "dining_chair_02", c => Lum(c) > 0.08f, "Upholstery", 0.5f, 0.35f, "Legs", 0.4f);

        static GameObject BuildArchedMirror()
        {
            const string id = "ornate_mirror_01";
            const string name = "Arched Mirror";
            var (root, model) = Begin(name, id);
            var r = model.GetComponentInChildren<Renderer>();
            // The gilt frame is saturated gold; the glass and its backing are neutral grey.
            SplitByTexture(id, r, ModelTex(id, "diff_1k.jpg"), c => Sat(c) > 0.25f);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            r.sharedMaterials = new[]
            {
                Lit($"{name} Frame", Tint(ModelTex(id, "diff_1k.jpg"), 0.7f), normal, Color.white, 0.55f, metallic: 0.3f),
                Lit("Mirror Glass", null, null, new Color(0.85f, 0.87f, 0.88f), 0.97f, metallic: 1f),
            };
            Zone(root, "Frame", ZoneColorSource.ItemRole, new PaintableZone.Target(r, 0));
            Zone(root, "Glass", ZoneColorSource.Fixed, new PaintableZone.Target(r, 1)).SetAllowOverride(false);
            // The scan hangs from its centre; stand it on the root like every other piece.
            var b = r.bounds;
            model.transform.position -= new Vector3(b.center.x, b.min.y, 0f);
            return Save(root, name);
        }

        static GameObject BuildApothecaryChest()
        {
            const string id = "vintage_wooden_drawer_01";
            const string name = "Apothecary Chest";
            var (root, model) = Begin(name, id);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            var body = Lit($"{name} Body", Tint(ModelTex(id, "diff_1k.jpg"), 0.7f), normal, Color.white, 0.4f);
            var drawers = Lit($"{name} Drawers", Tex(ModelTex(id, "diff_1k.jpg")), normal, Color.white, 0.45f);
            var bodyTargets = new List<PaintableZone.Target>();
            var drawerTargets = new List<PaintableZone.Target>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                bool drawer = r.name.Contains("_drawer");
                SetSlot(r, -1, drawer ? drawers : body);
                (drawer ? drawerTargets : bodyTargets).Add(new PaintableZone.Target(r));
            }
            // A painted carcass around the original oak-and-brass drawers.
            Zone(root, "Body", ZoneColorSource.ItemRole, bodyTargets.ToArray());
            Zone(root, "Drawer Fronts", ZoneColorSource.Fixed, drawerTargets.ToArray()).SetAllowOverride(false);
            return Save(root, name);
        }

        static GameObject BuildSlattedMediaUnit()
        {
            const string id = "modern_wooden_cabinet";
            const string name = "Slatted Media Unit";
            var (root, model) = Begin(name, id);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            var body = Lit($"{name} Body", Tint(ModelTex(id, "diff_1k.jpg"), 0.6f), normal, Color.white, 0.4f);
            var doors = Lit($"{name} Doors", Tex(ModelTex(id, "diff_1k.jpg")), normal, Color.white, 0.4f);
            var metal = Metal();
            var bodyTargets = new List<PaintableZone.Target>();
            var doorTargets = new List<PaintableZone.Target>();
            var fittings = new List<PaintableZone.Target>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("_door"))
                {
                    SetSlot(r, -1, doors);
                    doorTargets.Add(new PaintableZone.Target(r));
                }
                else if (r.name.Contains("_path"))
                {
                    SetSlot(r, -1, metal); // the sliding-door tracks
                    fittings.Add(new PaintableZone.Target(r));
                }
                else
                {
                    SetSlot(r, -1, body);
                    bodyTargets.Add(new PaintableZone.Target(r));
                }
            }
            // A painted case; the slatted oak doors are the point of the piece, so they stay oak.
            Zone(root, "Body", ZoneColorSource.ItemRole, bodyTargets.ToArray());
            Zone(root, "Doors", ZoneColorSource.Fixed, doorTargets.ToArray()).SetAllowOverride(false);
            if (fittings.Count > 0)
                Zone(root, "Fittings", ZoneColorSource.Fixed, fittings.ToArray()).SetAllowOverride(false);
            return Save(root, name);
        }

        static GameObject BuildCubeShelves()
        {
            const string id = "wooden_display_shelves_01";
            const string name = "Cube Shelves";
            var (root, model) = Begin(name, id);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            var tinted = Tint(ModelTex(id, "diff_1k.jpg"), 0.65f);
            var frame = Lit($"{name} Frame", tinted, normal, Color.white, 0.4f);
            var bins = Lit($"{name} Bins", tinted, normal, Color.white, 0.15f);
            var frameTargets = new List<PaintableZone.Target>();
            var binTargets = new List<PaintableZone.Target>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                bool bin = r.name.Contains("_drawer");
                SetSlot(r, -1, bin ? bins : frame);
                (bin ? binTargets : frameTargets).Add(new PaintableZone.Target(r));
            }
            Zone(root, "Frame", ZoneColorSource.ItemRole, frameTargets.ToArray());
            // The fabric bins carry the accent, like the cushions on a sofa.
            Zone(root, "Bins", ZoneColorSource.Slot, PaletteSlot.Accent, binTargets.ToArray());
            return Save(root, name);
        }

        static GameObject BuildPedestalTable()
        {
            const string id = "round_wooden_table_01";
            const string name = "Pedestal Table";
            var (root, model) = Begin(name, id);
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            var finish = Lit($"{name} Finish", Tint(ModelTex(id, "diff_1k.jpg"), 0.7f), normal, Color.white, 0.5f);
            var targets = new List<PaintableZone.Target>();
            Renderer bolts = null;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r.name.EndsWith("_bolts"))
                {
                    bolts = r;
                    continue;
                }
                SetSlot(r, -1, finish);
                targets.Add(new PaintableZone.Target(r));
            }
            Zone(root, "Finish", ZoneColorSource.ItemRole, targets.ToArray());
            if (bolts != null)
            {
                SetSlot(bolts, -1, Metal());
                Zone(root, "Fittings", ZoneColorSource.Fixed, new PaintableZone.Target(bolts)).SetAllowOverride(false);
            }
            return Save(root, name);
        }

        static GameObject BuildSucculent()
        {
            const string id = "potted_plant_04";
            const string name = "Succulent";
            var (root, model) = Begin(name, id);
            var tex = ModelTex(id, "diff_1k.jpg");
            var normal = Tex(ModelTex(id, "nor_gl_1k.exr"));
            var pot = Lit($"{name} Pot", Tint(tex, 0.6f), normal, Color.white, 0.35f);
            var natural = Lit($"{name} Plant", Tex(tex), normal, Color.white, 0.25f);
            var potTargets = new List<PaintableZone.Target>();
            var plantTargets = new List<PaintableZone.Target>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                bool isPot = r.name.EndsWith("_pot");
                SetSlot(r, -1, isPot ? pot : natural);
                (isPot ? potTargets : plantTargets).Add(new PaintableZone.Target(r));
            }
            Zone(root, "Pot", ZoneColorSource.ItemRole, potTargets.ToArray());
            Zone(root, "Leaves", ZoneColorSource.Fixed, plantTargets.ToArray()).SetAllowOverride(false);
            return Save(root, name);
        }

        /// <summary>
        /// Replaces the renderer's mesh with a copy split into two sub-meshes: triangles whose texels
        /// mostly satisfy <paramref name="first"/> go to sub-mesh 0, the rest to sub-mesh 1. Each triangle
        /// is judged at its centroid and halfway to each corner, so seams and thin strips vote sensibly.
        /// The split mesh is saved next to the model as {id}_split.asset.
        /// </summary>
        static void SplitByTexture(string id, Renderer r, string diffusePath, System.Func<Color, bool> first)
        {
            var filter = r.GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            var tex = LoadPixels(diffusePath);
            var uv = source.uv;
            var tris = source.triangles;
            var a = new List<int>();
            var b = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector2 u0 = uv[tris[i]], u1 = uv[tris[i + 1]], u2 = uv[tris[i + 2]];
                var c = (u0 + u1 + u2) / 3f;
                int votes = 0;
                foreach (var p in new[] { c, (c + u0) / 2f, (c + u1) / 2f, (c + u2) / 2f })
                    if (first(tex.GetPixelBilinear(p.x, p.y)))
                        votes++;
                var list = votes >= 2 ? a : b;
                list.Add(tris[i]);
                list.Add(tris[i + 1]);
                list.Add(tris[i + 2]);
            }
            Object.DestroyImmediate(tex);

            var split = Object.Instantiate(source);
            split.name = source.name + " (split)";
            split.subMeshCount = 2;
            split.SetTriangles(a, 0);
            split.SetTriangles(b, 1);
            split.RecalculateBounds();

            var path = $"{Models}/{id}/{id}_split.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(split, existing);
                Object.DestroyImmediate(split);
                split = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(split, path);
            }
            filter.sharedMesh = split;
            Debug.Log($"[PaletteRoom] {id}: split {a.Count / 3} painted / {b.Count / 3} natural triangles.");
        }

        static Material Metal() => Lit("Powder Coat Black", null, null, new Color(0.16f, 0.16f, 0.17f), 0.45f, metallic: 0.6f);

        /// <summary>
        /// The roll about +Z that squares up a cushion facing +Z: the one that makes its outline smallest,
        /// so its edges run along X and Y rather than sitting as a diamond.
        /// </summary>
        static Quaternion SquareUp(Mesh mesh, Vector3 scale)
        {
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = Vector3.Scale(verts[i], scale);
            int stride = Mathf.Max(1, verts.Length / 2000); // a sample of vertices is plenty for extents

            float Extent(Vector3 axis)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < verts.Length; i += stride)
                {
                    float d = Vector3.Dot(verts[i], axis);
                    lo = Mathf.Min(lo, d);
                    hi = Mathf.Max(hi, d);
                }
                return hi - lo;
            }

            // The cushion already faces +Z in its mesh space (it's puffy, so "thinnest direction" isn't
            // a reliable guide); only its roll within the face is off.
            var normal = Vector3.forward;

            // Roll within the face so the outline is smallest.
            var u0 = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 bestU = u0;
            float bestArea = float.MaxValue;
            for (float r = 0f; r < 90f; r += 0.5f)
            {
                var u = Quaternion.AngleAxis(r, normal) * u0;
                float area = Extent(u) * Extent(Vector3.Cross(normal, u));
                if (area < bestArea)
                {
                    bestArea = area;
                    bestU = u;
                }
            }
            var up = Vector3.Cross(normal, bestU);
            return Quaternion.Inverse(Quaternion.LookRotation(normal, up));
        }

        static (GameObject root, GameObject model) Begin(string name, string id)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{id}/{id}.fbx");
            if (fbx == null)
                throw new FileNotFoundException($"Missing model {id}; see Models/CREDITS.md");
            var root = new GameObject(name);
            var model = (GameObject)Object.Instantiate(fbx, root.transform, false);
            model.name = id;
            var fit = ModelFit[id];
            model.transform.localRotation = Quaternion.Euler(0f, fit.yaw, 0f) * model.transform.localRotation;
            model.transform.localScale *= fit.scale;
            return (root, model);
        }

        static PaintableZone Zone(GameObject root, string name, ZoneColorSource source, params PaintableZone.Target[] targets) =>
            Zone(root, name, source, PaletteSlot.Neutral, targets);

        static PaintableZone Zone(GameObject root, string name, ZoneColorSource source, PaletteSlot slot, params PaintableZone.Target[] targets)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var zone = go.AddComponent<PaintableZone>();
            zone.ConfigureExisting(name, source, slot, targets);
            return zone;
        }

        static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static int SlotOf(Renderer r, string nameContains, bool exact = false)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                    continue;
                if (exact ? mats[i].name == nameContains : mats[i].name.Contains(nameContains))
                    return i;
            }
            throw new System.InvalidOperationException($"{r.name} has no material matching '{nameContains}'");
        }

        static void SetSlot(Renderer r, int index, Material m)
        {
            var mats = r.sharedMaterials;
            if (index < 0)
            {
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = m;
            }
            else
            {
                mats[index] = m;
            }
            r.sharedMaterials = mats;
        }

        // ---- materials & textures ---------------------------------------------------------

        static string ModelTex(string id, string suffix) => $"{Models}/{id}/textures/{id}_{suffix}";

        static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null)
                throw new FileNotFoundException($"Missing texture {path}");
            return t;
        }

        static Material Lit(string name, Texture baseMap, Texture normalMap, Color color, float smoothness, float metallic = 0f, float tiling = 1f)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", baseMap);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BumpMap", normalMap);
            m.SetFloat("_BumpScale", 1f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            ApplyKeywords(m);
            return m;
        }

        /// <summary>Lets URP derive keywords and render state from the material's properties.</summary>
        static void ApplyKeywords(Material m)
        {
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
        }

        /// <summary>
        /// A grayscale copy of an albedo, normalised to a light mean, so the palette color (applied as a
        /// tint) shows true while the weave or grain stays visible. <paramref name="detail"/> scales how
        /// much of the original contrast is kept.
        /// </summary>
        static Texture2D Tint(string sourcePath, float detail)
        {
            var dst = Path.ChangeExtension(sourcePath, null) + "_tint.png";
            var src = LoadPixels(sourcePath);
            var px = src.GetPixels32();
            double sum = 0;
            var lum = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                lum[i] = (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) / 255f;
                sum += lum[i];
            }
            float mean = Mathf.Max((float)(sum / px.Length), 0.01f);
            const float target = 0.9f;
            for (int i = 0; i < px.Length; i++)
            {
                float v = Mathf.Clamp01(target * (1f + (lum[i] / mean - 1f) * detail));
                byte b = (byte)(v * 255f);
                px[i] = new Color32(b, b, b, 255);
            }
            src.SetPixels32(px);
            src.Apply();
            File.WriteAllBytes(dst, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(dst);
            SetImport(dst, normal: false, srgb: true);
            return Tex(dst);
        }

        /// <summary>A color copy of an albedo with its brightness multiplied by <paramref name="gain"/>.</summary>
        static Texture2D Brightened(string sourcePath, float gain)
        {
            var dst = Path.ChangeExtension(sourcePath, null) + "_bright.png";
            var src = LoadPixels(sourcePath);
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                px[i].r = (byte)Mathf.Min(255f, px[i].r * gain);
                px[i].g = (byte)Mathf.Min(255f, px[i].g * gain);
                px[i].b = (byte)Mathf.Min(255f, px[i].b * gain);
            }
            src.SetPixels32(px);
            src.Apply();
            File.WriteAllBytes(dst, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(dst);
            SetImport(dst, normal: false, srgb: true);
            return Tex(dst);
        }

        /// <summary>Packs a separate opacity map into the albedo's alpha channel for alpha clipping.</summary>
        static Texture2D WithAlpha(string diffusePath, string alphaPath)
        {
            var dst = Path.ChangeExtension(diffusePath, null) + "_rgba.png";
            var color = LoadPixels(diffusePath);
            var alpha = LoadPixels(alphaPath);
            var c = color.GetPixels32();
            var a = alpha.width == color.width ? alpha.GetPixels32() : Resized(alpha, color.width, color.height);
            for (int i = 0; i < c.Length; i++)
                c[i].a = a[i].r;
            color.SetPixels32(c);
            color.Apply();
            File.WriteAllBytes(dst, color.EncodeToPNG());
            Object.DestroyImmediate(color);
            Object.DestroyImmediate(alpha);
            AssetDatabase.ImportAsset(dst);
            SetImport(dst, normal: false, srgb: true, alphaIsTransparency: true);
            return Tex(dst);
        }

        static Color32[] Resized(Texture2D t, int w, int h)
        {
            var result = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[y * w + x] = t.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
            return result;
        }

        static Texture2D LoadPixels(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(assetPath)))
                throw new IOException($"Could not decode {assetPath}");
            return tex;
        }

        static void ConfigureTextureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Models, Textures }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(path);
                bool normal = file.Contains("_nor_gl");
                bool data = file.Contains("_rough") || file.Contains("_metal") || file.Contains("_alpha") || file.Contains("_opacity") || file.Contains("_ao");
                SetImport(path, normal, srgb: !normal && !data);
            }
        }

        static void SetImport(string path, bool normal, bool srgb, bool alphaIsTransparency = false)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            bool changed = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (!normal && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
            if (importer.maxTextureSize != 1024) { importer.maxTextureSize = 1024; changed = true; }
            if (importer.alphaIsTransparency != alphaIsTransparency) { importer.alphaIsTransparency = alphaIsTransparency; changed = true; }
            if (changed)
                importer.SaveAndReimport();
        }
    }
}
