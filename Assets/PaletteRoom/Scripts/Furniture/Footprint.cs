using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// A piece's outline on the floor: a rotated rectangle in the XZ plane. Overlap uses the
    /// separating-axis test, so rotated pieces are compared exactly rather than by their world bounds.
    /// </summary>
    public readonly struct Footprint
    {
        /// <summary>Pieces may touch; only real interpenetration counts.</summary>
        const float Tolerance = 0.005f;

        public readonly Vector2 centre;
        public readonly Vector2 axisX;
        public readonly Vector2 axisZ;
        public readonly Vector2 half;

        public Footprint(Vector2 centre, Vector2 axisX, Vector2 axisZ, Vector2 half)
        {
            this.centre = centre;
            this.axisX = axisX;
            this.axisZ = axisZ;
            this.half = half;
        }

        /// <summary>Footprint of a local box (e.g. a BoxCollider) placed at a pose; scale is ignored.</summary>
        public static Footprint FromBox(Vector3 localCentre, Vector3 localSize, Vector3 position, Quaternion rotation)
        {
            var c = position + rotation * localCentre;
            var x = rotation * Vector3.right;
            var z = rotation * Vector3.forward;
            return new Footprint(
                new Vector2(c.x, c.z),
                new Vector2(x.x, x.z).normalized,
                new Vector2(z.x, z.z).normalized,
                new Vector2(localSize.x, localSize.z) * 0.5f);
        }

        public bool Overlaps(Footprint other)
        {
            return !Separated(axisX, other) && !Separated(axisZ, other)
                && !Separated(other.axisX, other) && !Separated(other.axisZ, other);
        }

        bool Separated(Vector2 axis, Footprint other)
        {
            float distance = Mathf.Abs(Vector2.Dot(other.centre - centre, axis));
            return distance >= Radius(axis) + other.Radius(axis) - 2f * Tolerance;
        }

        float Radius(Vector2 axis) =>
            Mathf.Abs(Vector2.Dot(axisX, axis)) * half.x + Mathf.Abs(Vector2.Dot(axisZ, axis)) * half.y;
    }
}
