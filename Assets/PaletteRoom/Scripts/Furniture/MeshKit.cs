using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Procedural meshes for the code-built furniture: rounded boxes with real bevels, puffy cushions,
    /// and lathe-turned parts (tapered legs, lamp shades). UVs are in metres, so tiled fabric and wood
    /// textures keep a consistent real-world scale across pieces. Meshes are cached by shape.
    /// </summary>
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> s_Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Box of <paramref name="size"/> with edges rounded by <paramref name="radius"/>.
        /// <paramref name="puff"/> bulges the top and bottom faces outward like a filled cushion.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, float puff = 0f, int cornerSteps = 6)
        {
            string key = $"box{size.x:F3},{size.y:F3},{size.z:F3},{radius:F3},{puff:F3},{cornerSteps}";
            if (s_Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) - 0.0005f);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Six faces of a cube; every vertex is then pulled onto the rounded surface.
            AddFace(Vector3.up, Vector3.right, Vector3.forward);
            AddFace(Vector3.down, Vector3.right, Vector3.back);
            AddFace(Vector3.forward, Vector3.left, Vector3.up);
            AddFace(Vector3.back, Vector3.right, Vector3.up);
            AddFace(Vector3.right, Vector3.forward, Vector3.up);
            AddFace(Vector3.left, Vector3.back, Vector3.up);

            void AddFace(Vector3 n, Vector3 u, Vector3 v)
            {
                float hn = Mathf.Abs(Vector3.Dot(half, n)), hu = Mathf.Abs(Vector3.Dot(half, u)), hv = Mathf.Abs(Vector3.Dot(half, v));
                var us = AxisSamples(hu, radius, cornerSteps);
                var vs = AxisSamples(hv, radius, cornerSteps);
                int start = vertices.Count;
                foreach (float y in vs)
                {
                    foreach (float x in us)
                    {
                        var p = n * hn + u * x + v * y;
                        var inner = new Vector3(
                            Mathf.Clamp(p.x, -half.x + radius, half.x - radius),
                            Mathf.Clamp(p.y, -half.y + radius, half.y - radius),
                            Mathf.Clamp(p.z, -half.z + radius, half.z - radius));
                        var dir = (p - inner).sqrMagnitude > 1e-12f ? (p - inner).normalized : n;
                        var pos = inner + dir * radius;
                        if (puff > 0f && Mathf.Abs(n.y) > 0.5f)
                        {
                            // Filled-cushion bulge; tilt the normal to match its slope so shading stays smooth.
                            float a = x / hu, b = y / hv;
                            pos.y += Mathf.Sign(n.y) * puff * (1f - a * a) * (1f - b * b);
                            var slope = u * (2f * a * (1f - b * b) * puff / hu) + v * (2f * b * (1f - a * a) * puff / hv);
                            dir = (dir + slope * Mathf.Max(0f, Mathf.Abs(Vector3.Dot(dir, n)))).normalized;
                        }
                        vertices.Add(pos);
                        normals.Add(dir);
                        // Planar UVs in metres along the face's own axes.
                        uvs.Add(new Vector2(Vector3.Dot(pos, u), Vector3.Dot(pos, v)));
                    }
                }
                int row = us.Count;
                for (int j = 0; j < vs.Count - 1; j++)
                {
                    for (int i = 0; i < us.Count - 1; i++)
                    {
                        int k = start + j * row + i;
                        triangles.Add(k); triangles.Add(k + row); triangles.Add(k + 1);
                        triangles.Add(k + 1); triangles.Add(k + row); triangles.Add(k + row + 1);
                    }
                }
            }

            var mesh = Build(key, vertices, normals, uvs, triangles);
            mesh.RecalculateTangents();
            s_Cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Face coordinates from -half to +half. Inside each rounded edge the samples are spaced so they
        /// land at even angles around the bevel (the face covers 0–45°; the neighbouring face covers the
        /// rest), which keeps curved edges smooth without over-tessellating flat areas.
        /// </summary>
        static List<float> AxisSamples(float half, float radius, int cornerSteps)
        {
            var edge = new List<float>();
            for (int i = 0; i <= cornerSteps; i++)
            {
                float theta = i / (float)cornerSteps * Mathf.PI * 0.25f;
                edge.Add(half - radius + radius * Mathf.Tan(theta));
            }
            float flat = 2f * (half - radius);
            int interior = Mathf.Clamp(Mathf.CeilToInt(flat / 0.12f), 1, 12);

            var result = new List<float>();
            for (int i = edge.Count - 1; i >= 0; i--)
                result.Add(-edge[i]);
            for (int i = 1; i < interior; i++)
                result.Add(-(half - radius) + flat * i / interior);
            foreach (float e in edge)
                result.Add(e);
            return result;
        }

        /// <summary>
        /// Surface of revolution around Y. <paramref name="profile"/> is (radius, height) from bottom to
        /// top. Closed ends get caps when <paramref name="capEnds"/> is set; shades stay open.
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int segments = 24, bool capEnds = true)
        {
            var sb = new System.Text.StringBuilder("lathe");
            foreach (var p in profile)
                sb.Append($"{p.x:F3},{p.y:F3};");
            sb.Append(segments).Append(capEnds);
            string key = sb.ToString();
            if (s_Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float length = 0f;
            var along = new float[profile.Length];
            for (int i = 1; i < profile.Length; i++)
            {
                length += Vector2.Distance(profile[i], profile[i - 1]);
                along[i] = length;
            }

            for (int i = 0; i < profile.Length; i++)
            {
                // Profile tangent → outward normal in the radial plane.
                var prev = profile[Mathf.Max(0, i - 1)];
                var next = profile[Mathf.Min(profile.Length - 1, i + 1)];
                var t = (next - prev).normalized;
                var n2 = new Vector2(t.y, -t.x);
                float circumference = 2f * Mathf.PI * Mathf.Max(profile[i].x, 0.01f);
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    vertices.Add(radial * profile[i].x + Vector3.up * profile[i].y);
                    normals.Add((radial * n2.x + Vector3.up * n2.y).normalized);
                    uvs.Add(new Vector2(s / (float)segments * circumference, along[i]));
                }
            }

            int ring = segments + 1;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int k = i * ring + s;
                    triangles.Add(k); triangles.Add(k + ring); triangles.Add(k + 1);
                    triangles.Add(k + 1); triangles.Add(k + ring); triangles.Add(k + ring + 1);
                }
            }

            if (capEnds)
            {
                Cap(profile[0], Vector3.down, true);
                Cap(profile[profile.Length - 1], Vector3.up, false);
            }

            void Cap(Vector2 p, Vector3 normal, bool bottom)
            {
                if (p.x <= 0.0001f)
                    return;
                int centre = vertices.Count;
                vertices.Add(Vector3.up * p.y);
                normals.Add(normal);
                uvs.Add(Vector2.zero);
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    var v = new Vector3(Mathf.Cos(ang) * p.x, p.y, Mathf.Sin(ang) * p.x);
                    vertices.Add(v);
                    normals.Add(normal);
                    uvs.Add(new Vector2(v.x, v.z));
                }
                for (int s = 0; s < segments; s++)
                {
                    if (bottom)
                    {
                        triangles.Add(centre); triangles.Add(centre + 1 + s); triangles.Add(centre + 2 + s);
                    }
                    else
                    {
                        triangles.Add(centre); triangles.Add(centre + 2 + s); triangles.Add(centre + 1 + s);
                    }
                }
            }

            var mesh = Build(key, vertices, normals, uvs, triangles);
            mesh.RecalculateTangents();
            s_Cache[key] = mesh;
            return mesh;
        }

        /// <summary>A leg that narrows toward the floor, as on most Scandinavian furniture.</summary>
        public static Mesh TaperedLeg(float height, float topRadius, float bottomRadius) =>
            Lathe(new[] { new Vector2(bottomRadius, 0f), new Vector2(topRadius, height) }, 14);

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
