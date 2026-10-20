using UnityEngine;

namespace PaletteRoom
{
    /// <summary>60-30-10 role of a furniture item.</summary>
    public enum FurnitureRole
    {
        Dominant,
        Secondary,
        Accent,
    }

    /// <summary>Which surface an item rests on, which decides how it moves when grabbed.</summary>
    public enum FurnitureMount
    {
        /// <summary>Slides on the floor and turns around the vertical axis only.</summary>
        Floor,
        /// <summary>Moves freely in height but stays upright, like a hung picture.</summary>
        Wall,
        /// <summary>Small soft items (pillows) that can go anywhere at any angle.</summary>
        Free,
    }

    /// <summary>A placed piece of furniture with a role tag and 1-3 paintable zones.</summary>
    public class FurnitureItem : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] string displayName;
        [SerializeField] FurnitureKind kind;
        [SerializeField] string variantId;
        [SerializeField] FurnitureRole role;
        [SerializeField] FurnitureMount mount = FurnitureMount.Floor;

        PaintableZone[] m_Zones;

        public string ItemId => itemId;
        public FurnitureKind Kind => kind;
        public string VariantId => variantId;
        public string DisplayName => displayName;
        public FurnitureRole Role => role;
        public FurnitureMount Mount => mount;

        public PaintableZone[] Zones
        {
            get
            {
                if (m_Zones == null || m_Zones.Length == 0)
                    m_Zones = GetComponentsInChildren<PaintableZone>(true);
                return m_Zones;
            }
        }

        /// <summary>
        /// Floor-standing solids can't share floor space. Rugs lie under furniture, and pillows and
        /// wall art rest on other pieces, so those never block.
        /// </summary>
        public bool BlocksFloor => mount == FurnitureMount.Floor && kind != FurnitureKind.Rug;

        /// <summary>
        /// This piece's floor outline if it stood at <paramref name="position"/> / <paramref name="rotation"/>:
        /// one rectangle per box collider on the root, so L-shaped pieces (a sofa with a chaise) leave
        /// their notch usable.
        /// </summary>
        public Footprint[] FootprintsAt(Vector3 position, Quaternion rotation)
        {
            var boxes = GetComponents<BoxCollider>();
            var result = new Footprint[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
                result[i] = Footprint.FromBox(boxes[i].center, boxes[i].size, position, rotation);
            return result;
        }

        public Footprint[] CurrentFootprints => FootprintsAt(transform.position, transform.rotation);

        public static bool Overlap(Footprint[] a, Footprint[] b)
        {
            foreach (var fa in a)
            {
                foreach (var fb in b)
                {
                    if (fa.Overlaps(fb))
                        return true;
                }
            }
            return false;
        }

        public void Configure(FurnitureKind kind, string variantId, string itemId, string displayName, FurnitureRole role, FurnitureMount mount)
        {
            this.kind = kind;
            this.variantId = variantId;
            this.itemId = itemId;
            this.displayName = displayName;
            this.role = role;
            this.mount = mount;
            m_Zones = null;
        }

        void OnEnable()
        {
            if (PaletteManager.Instance != null)
                PaletteManager.Instance.Register(this);
        }

        void OnDisable()
        {
            if (PaletteManager.Instance != null)
                PaletteManager.Instance.Unregister(this);
        }
    }
}
